using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using TiltMaze.App.Services;
using TiltMaze.Core;
using Cell = TiltMaze.Core.Cell;
using MazeLevel = TiltMaze.Core.Level;

namespace TiltMaze.App.Components.Pages;

public partial class Game : IDisposable
{
    [Parameter] public int Level { get; set; }

    [Inject] private Scores scores { get; set; } = null!;
    [Inject] private TiltSensor tilt { get; set; } = null!;
    [Inject] private NavigationManager navigation { get; set; } = null!;

    private ElementReference board;
    private MazeLevel maze = null!;
    private Ball ball = null!;
    private string wallPath = "";
    private int loadedLevel;

    private double time;
    private double? best;
    private int falls;
    private bool paused;
    private bool finished;
    private bool newBest;
    private bool needFocus;
    private string hint = "";

    // Taster der holdes nede
    private bool left, right, up, down;

    private CancellationTokenSource? stop;
    private Window? window;

    // Kører når siden åbnes og ved skift til en anden bane
    protected override void OnInitialized()
    {
        hint = tilt.IsSupported ? "Tilt your phone to roll the ball. You can also use the arrow keys or WASD." : "Use the arrow keys or WASD to roll the ball. P pauses.";
    }

    protected override void OnParametersSet()
    {
        if (Level < 1)
        {
            navigation.NavigateTo("");
            return;
        }
        if (Level != loadedLevel)
            LoadLevel();
    }

    private void LoadLevel()
    {
        loadedLevel = Level;
        maze = new MazeLevel(Level);
        ball = new Ball(maze.Segments, maze.Start.X + 0.5, maze.Start.Y + 0.5);
        wallPath = BuildWallPath();
        best = scores.Best(Level);
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
        // Efter "Next level" mistede banen fokus, så tastaturet skal have det igen
        if (needFocus)
        {
            needFocus = false;
            await board.FocusAsync();
        }

        if (!firstRender)
            return;

        tilt.Start();

        // Pause når appen skjules
        window = Application.Current?.Windows.FirstOrDefault();
        if (window != null)
            window.Stopped += OnWindowStopped;

        await board.FocusAsync();

        stop = new CancellationTokenSource();
        _ = RunLoop(stop.Token);
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

    // Ét billede: flyt kuglen og tjek hul og mål
    private void Tick(double seconds)
    {
        // Taster giver -1, 0 eller 1. Vip fra telefonen lægges til.
        double ax = (right ? 1 : 0) - (left ? 1 : 0) + tilt.X;
        double ay = (down ? 1 : 0) - (up ? 1 : 0) + tilt.Y;
        ax = Math.Clamp(ax, -1, 1);
        ay = Math.Clamp(ay, -1, 1);

        ball.Update(seconds, ax, ay);
        time += seconds;

        foreach (Cell hole in maze.Holes)
        {
            if (Distance(hole) < 0.25)
            {
                Fall();
                return;
            }
        }

        if (Distance(maze.Goal) < 0.25)
            Finish();
    }

    // Afstand fra kuglen til midten af en celle
    private double Distance(Cell cell)
    {
        double dx = ball.X - (cell.X + 0.5);
        double dy = ball.Y - (cell.Y + 0.5);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // Kuglen faldt i et hul: tilbage til start
    private void Fall()
    {
        falls++;
        ball.Reset(maze.Start.X + 0.5, maze.Start.Y + 0.5);

        try
        {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(120));
        }
        catch (Exception)
        {
            // Computeren kan ikke vibrere
        }
    }

    private void Finish()
    {
        finished = true;
        newBest = scores.Save(Level, time);
        best = scores.Best(Level);
    }

    private void KeyDown(KeyboardEventArgs e)
    {
        SetKey(e.Key, true);
        if (e.Key == "p" || e.Key == "P" || e.Key == "Escape")
            _ = TogglePause();
    }

    private void KeyUp(KeyboardEventArgs e)
    {
        SetKey(e.Key, false);
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

    private async Task Retry()
    {
        LoadLevel();
        await board.FocusAsync();
    }

    private void NextLevel()
    {
        needFocus = true;
        navigation.NavigateTo("play/" + (Level + 1));
    }

    // Vinduet blev skjult eller minimeret
    private void OnWindowStopped(object? sender, EventArgs e)
    {
        paused = true;
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        stop?.Cancel();
        tilt.Stop();
        if (window != null)
            window.Stopped -= OnWindowStopped;
    }
}
