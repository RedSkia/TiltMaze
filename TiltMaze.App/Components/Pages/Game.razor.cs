using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using TiltMaze.App.Components.Play;
using TiltMaze.App.Services;
using TiltMaze.Core;

namespace TiltMaze.App.Components.Pages;

public partial class Game : IDisposable
{
    [Parameter] public string DifficultyName { get; set; } = "";
    [Parameter] public int? Seed { get; set; }

    [Inject] private Scores scores { get; set; } = null!;
    [Inject] private Wallet wallet { get; set; } = null!;
    [Inject] private Api api { get; set; } = null!;
    [Inject] private Auth auth { get; set; } = null!;
    [Inject] private TiltSensor tilt { get; set; } = null!;
    [Inject] private NavigationManager navigation { get; set; } = null!;
    [Inject] private IJSRuntime js { get; set; } = null!;

    // Kameraet følger kuglen, når hele banen ikke kan vises med mindst MinCell pixels pr. celle.
    // Så vises den med CameraCell pixels pr. celle.
    private const double MinCell = 40;
    private const double CameraCell = 56;

    // Nedtælling 3-2-1 før start: hvert tal står Beat sekunder, "Go!" står de første 0,6 s af løbet
    private const double Beat = 0.7;

    private ElementReference board;
    private ElementReference dialogButton;   // første knap i den dialog, der er åben
    private Joystick? joystick;
    private Course maze = null!;   // null indtil siden har fået en gyldig sværhedsgrad og seed
    private Ball ball = null!;
    private Camera camera = null!;
    private double time;      // fysik-tid: antal fysik-skridt x Ball.Step
    private int steps;        // antal fysik-skridt i løbet (også efter et fald)
    private double countdown;
    private double? best;
    private double? oldBest;     // bedste tid før dette løb, til "vs your best"
    private int falls;
    private bool paused;
    private bool finished;
    private bool newBest;
    private bool isDaily;
    private bool needFocus;           // banen skal have tastatur-fokus efter næste tegning
    private bool focusDialog;          // dialogens første knap skal have fokus efter næste tegning
    private bool started;              // spil-løkken er sat i gang
    private bool disposed;
    private string hint = "";
    private Outcome outcome;

    // Banens størrelse i pixels (fra JavaScript) og om kameraet følger kuglen
    private double boardWidth, boardHeight;
    private bool follow;
    private DotNetObjectReference<Game>? self;

    // Spøgelset: bedste løb på denne labyrint, og optagelsen af det løb vi spiller nu (x, y hvert 1/30 sekund)
    private Ghost? ghost;
    private readonly List<float> recording = new();
    private bool showGhost;
    private double ghostX, ghostY;
    private double? delta;            // forskel til spøgelset ved sidste nye celle (minus = foran)
    private int lastCellX, lastCellY;

    // Mønter: coinMask er dem der blev samlet op i tidligere løb (tegnes svagt), picked dem i dette løb (tegnes ikke).
    // runCoins er mønterne dette løb har givet (kun nye mønter), completionReward belønningen for at klare labyrinten.
    private int coinMask;
    private bool[] picked = [];
    private int runCoins;
    private int completionReward;
    private BallColor ballColor = Rewards.BallColors[0];
    private double popX, popY;
    private int pops;   // tæller op ved hver mønt, så "pop"-ringen starter forfra

    // Andres spøgelser (top 3 fra serveren): hentes i baggrunden ved start, og springes over uden net eller login
    private class Rival
    {
        public string Name = "";
        public Ghost Ghost = null!;
        public string Color = "";
        public bool Show;
        public double X, Y;
    }

    private readonly List<Rival> rivals = new();
    private static readonly string[] rivalColors = ["#b794f6", "#f4a3c4", "#8fb4ff"];

    // Taster der holdes nede
    private bool left, right, up, down;

    private bool playing => !paused && !finished;
    private bool modalOpen => paused || finished;   // en dialog er åben, så resten af skærmen er "inert"

    private CancellationTokenSource? stop;
    private Window? window;

    // Konfetti ved mål: 28 stykker med hver sin plads, forsinkelse og drejning
    private static readonly string[] confetti = Enumerable.Range(0, 28)
        .Select(i => "--x:" + (i * 37 % 100) + "%;--delay:" + N(i % 7 * 0.08) + "s;--turn:" + ((i % 2 == 0 ? 1 : -1) * (200 + i * 20)) + "deg")
        .ToArray();

    // Udsnittet af banen, som SVG'en viser. Uden kamera er det hele banen.
    private string ViewBox => N(camera.Left) + " " + N(camera.Top) + " " + N(camera.ViewWidth) + " " + N(camera.ViewHeight);

    // To ens animationer med hver sit navn: når klassen skifter ved et nyt fald, starter rystelsen forfra
    private string ShakeClass => falls == 0 ? "" : falls % 2 == 0 ? "shake-a" : "shake-b";

    private string? CountText
    {
        get
        {
            if (countdown > 0)
                return ((int)Math.Ceiling(countdown / Beat)).ToString();
            if (time < 0.6 && falls == 0)
                return "Go!";
            return null;
        }
    }

    // Kører når siden åbnes
    protected override void OnInitialized()
    {
        hint = tilt.IsSupported
            ? "Tilt your phone, or touch and drag anywhere on the board. Holes pull the ball in; roll fast to jump them."
            : "Use the arrow keys, WASD, or drag anywhere on the board with the mouse. Holes pull the ball in; roll fast to jump them. P pauses.";
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
        camera = new Camera(maze.Width, maze.Height);
        UpdateView();
        camera.Snap(ball.X, ball.Y);

        best = scores.Best(difficulty, seed);
        float[]? samples = scores.LoadGhost(difficulty, seed);
        ghost = samples == null ? null : new Ghost(samples, Ghost.Rate, maze.Width, maze.Height);
        recording.Clear();
        rivals.Clear();
        _ = LoadRivals(difficulty, seed);
        showGhost = false;
        delta = null;
        lastCellX = maze.Start.X;
        lastCellY = maze.Start.Y;

        isDaily = seed == Course.DailySeed();
        time = 0;
        steps = 0;
        countdown = Beat * 3;
        falls = 0;
        paused = false;
        finished = false;
        newBest = false;

        coinMask = wallet.CoinMask(difficulty, seed);
        picked = new bool[maze.Coins.Count];
        runCoins = 0;
        completionReward = 0;
        ballColor = wallet.Equipped;
    }

    private async Task LoadRivals(Difficulty difficulty, int seed)
    {
        try
        {
            await auth.Load();
            if (!auth.IsLoggedIn || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;

            var races = await api.GetTopGhosts(difficulty, seed);
            if (disposed || maze.Difficulty != difficulty || maze.Seed != seed)
                return;   // spillet er gået videre til en anden labyrint

            rivals.Clear();
            foreach (var race in races)
            {
                var rivalGhost = new Ghost(race.Samples, Ghost.Rate, maze.Width, maze.Height);
                rivals.Add(new Rival { Name = race.Username, Ghost = rivalGhost, Color = rivalColors[rivals.Count % rivalColors.Length] });
            }
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception)
        {
            // Fx et ødelagt svar. Så kører man bare uden andres spøgelser
        }
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

        // Fokus flyttes først, når dialogen eller banen faktisk er tegnet (og ikke længere "inert")
        if (focusDialog)
        {
            focusDialog = false;
            await dialogButton.FocusAsync();
        }
        else if (needFocus)
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
        self = DotNetObjectReference.Create(this);

        await js.InvokeVoidAsync("tiltMaze.watchSize", board, self);
        await board.FocusAsync();
    }

    // JavaScript kalder denne, når banens størrelse ændres
    [JSInvokable]
    public void BoardResized(double width, double height)
    {
        boardWidth = width;
        boardHeight = height;
        UpdateView();
        InvokeAsync(StateHasChanged);
    }

    // Vælger mellem hele banen og et kamera, der følger kuglen
    private void UpdateView()
    {
        double fullWidth = maze.Width + Camera.Margin * 2;
        double fullHeight = maze.Height + Camera.Margin * 2;
        double cell = Math.Min(boardWidth / fullWidth, boardHeight / fullHeight);

        follow = boardWidth > 0 && boardHeight > 0 && cell < MinCell;
        if (follow)
            camera.SetView(boardWidth / CameraCell, boardHeight / CameraCell);
        else
            camera.SetView(fullWidth, fullHeight);
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
                {
                    // En dialog er åben: slip joysticken, så kuglen ikke ruller af sig selv, når man fortsætter
                    joystick?.Release();
                    continue;
                }

                Tick(seconds);
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // Siden blev lukket
        }
        catch (Exception ex)
        {
            // Fx hvis siden er lukket midt i et billede. Løkken stopper, men appen går ikke ned
            Debug.WriteLine("RunLoop stoppede: " + ex.Message);
        }
    }

    // Ét billede: flyt kuglen (hul og mål tjekkes efter hvert fysik-skridt), spøgelset og kameraet
    private void Tick(double seconds)
    {
        camera.Follow(ball.X, ball.Y, seconds);

        // Under nedtællingen står kuglen stille, og uret går ikke
        if (countdown > 0)
        {
            countdown -= seconds;
            return;
        }

        // Taster giver -1, 0 eller 1. Vip fra telefonen og joysticken lægges til.
        double ax = (right ? 1 : 0) - (left ? 1 : 0) + tilt.X;
        double ay = (down ? 1 : 0) - (up ? 1 : 0) + tilt.Y;
        if (joystick != null)
        {
            ax += joystick.X;
            ay += joystick.Y;
        }
        ax = Math.Clamp(ax, -1, 1);
        ay = Math.Clamp(ay, -1, 1);

        outcome = Outcome.None;
        ball.Update(seconds, ax, ay, CheckOutcome);
        CollectCoins();
        MoveGhost();
        UpdateDelta();

        if (outcome == Outcome.Fell)
            Fall();
        else if (outcome == Outcome.Won)
            Finish();
    }

    // Kaldes efter hvert fysik-skridt. Tiden og optagelsen følger fysikken og ikke billederne,
    // så langsomme billeder ikke giver dobbelte målinger.
    private bool CheckOutcome()
    {
        steps++;
        time = steps * Ball.Step;
        Record();
        outcome = maze.Check(ball);
        return outcome != Outcome.None;
    }

    // Gemmer kuglens plads, hver gang fysik-tiden passerer et nyt 1/30 sekund
    private void Record()
    {
        while (recording.Count < ((int)(time * Ghost.Rate + 0.0001) + 1) * 2)
        {
            recording.Add((float)ball.X);
            recording.Add((float)ball.Y);
        }
    }

    // Rører kuglen en mønt, tages den. Giver kun en mønt i pungen første gang på labyrinten (Wallet).
    // Mønterne påvirker ikke fysikken, tiden eller optagelsen.
    private void CollectCoins()
    {
        for (int i = 0; i < picked.Length; i++)
        {
            if (picked[i] || !maze.TouchesCoin(ball, i))
                continue;

            picked[i] = true;
            if (wallet.TakeCoin(maze.Difficulty, maze.Seed, i))
                runCoins++;
            popX = maze.Coins[i].X + 0.5;
            popY = maze.Coins[i].Y + 0.5;
            pops++;
            if (Vibration.Default.IsSupported)
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(15));
        }
    }

    private bool IsOldCoin(int i)
    {
        return (coinMask >> i & 1) == 1;
    }

    private void MoveGhost()
    {
        showGhost = ghost != null && ghost.TryGetPosition(time, out ghostX, out ghostY);
        foreach (var rival in rivals)
            rival.Show = rival.Ghost.TryGetPosition(time, out rival.X, out rival.Y);
    }

    // Mellemtid: når kuglen kommer ind i en ny celle, sammenlignes tiden med, hvornår spøgelset var der
    private void UpdateDelta()
    {
        int cellX = (int)ball.X;
        int cellY = (int)ball.Y;
        if (cellX == lastCellX && cellY == lastCellY)
            return;

        lastCellX = cellX;
        lastCellY = cellY;
        double? ghostTime = ghost?.FirstVisit(cellX, cellY);
        if (ghostTime > 0)
            delta = time - ghostTime;
    }

    // Kuglen faldt i et hul: tilbage til start (skærmen ryster og blinker rødt, se CSS)
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
        focusDialog = true;
        oldBest = best;
        newBest = scores.Save(maze.Difficulty, maze.Seed, time, recording.ToArray());
        best = scores.Best(maze.Difficulty, maze.Seed);
        completionReward = wallet.Complete(maze.Difficulty, maze.Seed);
        if (Vibration.Default.IsSupported)
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(40));
    }

    private void KeyDown(KeyboardEventArgs e)
    {
        SetKey(e.Key, true);
        if (!e.Repeat && (e.Key == "p" || e.Key == "P" || e.Key == "Escape"))
            TogglePause();
    }

    private void KeyUp(KeyboardEventArgs e)
    {
        SetKey(e.Key, false);
    }

    // Esc i pausemenuen: fortsæt
    private void OverlayKey(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && !e.Repeat)
            TogglePause();
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

    private void TogglePause()
    {
        if (finished)
            return;
        paused = !paused;
        if (paused)
            focusDialog = true;
        else
            needFocus = true;
    }

    // Pausemenuen er den eneste menu: Resume, Retry eller Leave (Leave er selv bekræftelsen)
    private void Leave()
    {
        navigation.NavigateTo("");
    }

    // Samme labyrint igen (samme seed)
    private void Retry()
    {
        LoadMaze(maze.Difficulty, maze.Seed);
        needFocus = true;
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
        if (!finished && !paused)
        {
            paused = true;
            focusDialog = true;
        }
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
        self?.Dispose();
        if (window != null)
        {
            window.Stopped -= OnWindowStopped;
            window.Resumed -= OnWindowResumed;
        }
    }
}
