using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TiltMaze.App.Services;
using TiltMaze.Core;

namespace TiltMaze.App.Components.Pages;

public partial class Game : IDisposable
{
    [Parameter] public string DifficultyName { get; set; } = "";
    [Parameter] public int? Seed { get; set; }

    [Inject] private Scores scores { get; set; } = null!;
    [Inject] private TiltSensor tilt { get; set; } = null!;
    [Inject] private NavigationManager navigation { get; set; } = null!;

    private ElementReference board;
    private Course maze = null!;   // null indtil siden har fået en gyldig sværhedsgrad og seed
    private Ball ball = null!;
    private string wallPath = "";
    private double time;
    private double? best;
    private int falls;
    private bool paused;
    private bool finished;
    private bool newBest;
    private bool isDaily;
    private bool needFocus;
    private bool started;     // spil-løkken er sat i gang
    private bool disposed;
    private string hint = "";
    private Outcome outcome;

    // Spøgelset: bedste løb på denne labyrint, og optagelsen af det løb vi spiller nu (x, y hvert 1/30 sekund)
    private float[]? ghost;
    private readonly List<float> recording = new();
    private bool showGhost;
    private double ghostX, ghostY;

    // Taster der holdes nede
    private bool left, right, up, down;

    private bool playing => !paused && !finished;

    private CancellationTokenSource? stop;
    private Window? window;

    // Kører når siden åbnes
    protected override void OnInitialized()
    {
        hint = tilt.IsSupported
            ? "Tilt your phone to roll the ball. Roll fast to jump over holes. You can also use the arrow keys or WASD."
            : "Use the arrow keys or WASD to roll the ball. Roll fast to jump over holes. P pauses.";
    }

    // Kører når siden åbnes og når adressen ændres (ny seed)
    protected override void OnParametersSet()
    {
        // Ugyldig sværhedsgrad: tilbage til menuen. Manglende eller negativ seed: vælg en tilfældig.
        if (Enum.GetNames<Difficulty>().Contains(DifficultyName, StringComparer.OrdinalIgnoreCase) == false)
        {
            navigation.NavigateTo("", replace: true);
            return;
        }
        var difficulty = Enum.Parse<Difficulty>(DifficultyName, true);
        if (Seed == null || Seed < 0)
        {
            navigation.NavigateTo(Address(difficulty, Course.NewSeed()), replace: true);
            return;
        }

        if (maze == null || maze.Difficulty != difficulty || maze.Seed != Seed)
            LoadMaze(difficulty, Seed.Value);
    }

    private static string Address(Difficulty difficulty, int seed)
    {
        return "play/" + difficulty.ToString().ToLower() + "/" + seed;
    }

    private void LoadMaze(Difficulty difficulty, int seed)
    {
        maze = new Course(difficulty, seed);
        ball = new Ball(maze.Segments, maze.Start.X + 0.5, maze.Start.Y + 0.5);
        wallPath = BuildWallPath();
        best = scores.Best(difficulty, seed);
        ghost = scores.LoadGhost(difficulty, seed);
        recording.Clear();
        showGhost = false;
        isDaily = seed == Course.DailySeed();
        time = 0;
        falls = 0;
        paused = false;
        finished = false;
        newBest = false;
    }

    // Alle vægge i ét SVG-stykke, så Blazor kun skal sammenligne én tekst pr. billede
    private string BuildWallPath()
    {
        var text = new StringBuilder();
        foreach (Segment wall in maze.Segments)
            text.Append($"M{N(wall.X1)} {N(wall.Y1)}L{N(wall.X2)} {N(wall.Y2)}");
        return text.ToString();
    }

    // Tal til SVG skal have punktum, uanset sprog på computeren
    private static string N(double number)
    {
        return number.ToString("0.###", CultureInfo.InvariantCulture);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed || maze == null)
            return;

        // Efter "New maze" mistede banen fokus, så tastaturet skal have det igen
        if (needFocus)
        {
            needFocus = false;
            await board.FocusAsync();
        }

        if (started)
            return;
        started = true;

        // Alt her sker før første await, så Dispose altid kan rydde op
        tilt.Start();
        window = Application.Current?.Windows.FirstOrDefault();
        if (window != null)
        {
            window.Stopped += OnWindowStopped;
            window.Resumed += OnWindowResumed;
        }
        stop = new CancellationTokenSource();
        _ = RunLoop(stop.Token);

        await board.FocusAsync();
    }

    // Spil-løkken: ca. 60 gange i sekundet flyttes kuglen, og banen tegnes igen
    private async Task RunLoop(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
        var clock = Stopwatch.StartNew();
        double last = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                double now = clock.Elapsed.TotalSeconds;
                double seconds = Math.Min(now - last, 0.05);   // aldrig mere end 50 ms ad gangen
                last = now;
                if (paused || finished)
                    continue;

                Tick(seconds);
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // Siden blev lukket
        }
    }

    // Ét billede: flyt kuglen (hul og mål tjekkes efter hvert fysik-skridt) og opdater spøgelset
    private void Tick(double seconds)
    {
        // Taster giver -1, 0 eller 1. Vip fra telefonen lægges til.
        double ax = (right ? 1 : 0) - (left ? 1 : 0) + tilt.X;
        double ay = (down ? 1 : 0) - (up ? 1 : 0) + tilt.Y;
        ax = Math.Clamp(ax, -1, 1);
        ay = Math.Clamp(ay, -1, 1);

        outcome = Outcome.None;
        ball.Update(seconds, ax, ay, CheckOutcome);
        time += seconds;
        Record();
        MoveGhost();

        if (outcome == Outcome.Fell)
            Fall();
        else if (outcome == Outcome.Won)
            Finish();
    }

    private bool CheckOutcome()
    {
        outcome = maze.Check(ball);
        return outcome != Outcome.None;
    }

    // Gemmer kuglens plads hvert 1/30 sekund
    private void Record()
    {
        while (recording.Count < ((int)(time * Scores.GhostRate) + 1) * 2)
        {
            recording.Add((float)ball.X);
            recording.Add((float)ball.Y);
        }
    }

    // Spøgelset står mellem to målinger, så den glider jævnt
    private void MoveGhost()
    {
        showGhost = false;
        if (ghost == null)
            return;

        double position = time * Scores.GhostRate;
        int i = (int)position;
        if ((i + 1) * 2 + 1 >= ghost.Length)
            return;   // optagelsen er slut

        double part = position - i;
        ghostX = ghost[i * 2] + (ghost[i * 2 + 2] - ghost[i * 2]) * part;
        ghostY = ghost[i * 2 + 1] + (ghost[i * 2 + 3] - ghost[i * 2 + 1]) * part;
        showGhost = true;
    }

    // Kuglen faldt i et hul: tilbage til start
    private void Fall()
    {
        falls++;
        ball.Reset(maze.Start.X + 0.5, maze.Start.Y + 0.5);
        if (Vibration.Default.IsSupported)
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(120));
    }

    private void Finish()
    {
        finished = true;
        newBest = scores.Save(maze.Difficulty, maze.Seed, time);
        if (newBest)
            scores.SaveGhost(maze.Difficulty, maze.Seed, recording.ToArray());
        best = scores.Best(maze.Difficulty, maze.Seed);
    }

    private void KeyDown(KeyboardEventArgs e)
    {
        SetKey(e.Key, true);
        if (!e.Repeat && (e.Key == "p" || e.Key == "P" || e.Key == "Escape"))
            _ = TogglePause();
    }

    private void KeyUp(KeyboardEventArgs e)
    {
        SetKey(e.Key, false);
    }

    // Banen mistede fokus (fx alt-tab): ellers bliver en holdt tast ved med at være "nede"
    private void ClearKeys()
    {
        left = right = up = down = false;
    }

    private void SetKey(string key, bool pressed)
    {
        switch (key.ToLower())
        {
            case "arrowleft":
            case "a":
                left = pressed;
                break;
            case "arrowright":
            case "d":
                right = pressed;
                break;
            case "arrowup":
            case "w":
                up = pressed;
                break;
            case "arrowdown":
            case "s":
                down = pressed;
                break;
        }
    }

    private async Task TogglePause()
    {
        if (finished)
            return;
        paused = !paused;
        await board.FocusAsync();
    }

    // Samme labyrint igen (samme seed)
    private async Task Retry()
    {
        LoadMaze(maze.Difficulty, maze.Seed);
        await board.FocusAsync();
    }

    // En ny labyrint med tilfældig seed i samme sværhedsgrad
    private void NewMaze()
    {
        needFocus = true;
        navigation.NavigateTo(Address(maze.Difficulty, Course.NewSeed()));
    }

    // Vinduet blev skjult eller minimeret: pause og sluk for sensoren. Spillet fortsætter først, når man trykker Resume.
    private void OnWindowStopped(object? sender, EventArgs e)
    {
        paused = true;
        tilt.Stop();
        InvokeAsync(StateHasChanged);
    }

    private void OnWindowResumed(object? sender, EventArgs e)
    {
        if (!disposed)
            tilt.Start();
    }

    public void Dispose()
    {
        disposed = true;
        stop?.Cancel();
        tilt.Stop();
        if (window != null)
        {
            window.Stopped -= OnWindowStopped;
            window.Resumed -= OnWindowResumed;
        }
    }
}
