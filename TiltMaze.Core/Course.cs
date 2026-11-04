namespace TiltMaze.Core;

// Et linjestykke (en væg) i banens koordinater. 1 enhed = 1 celle.
public record struct Segment(double X1, double Y1, double X2, double Y2);

// Et hul i midten af en celle. Store huller (radius 0,46) fylder hele gangen, små er smalle nok til at rulle forbi.
public record struct Hole(Cell Cell, double Radius)
{
    public const double BigRadius = 0.46;

    public double CenterX => Cell.X + 0.5;
    public double CenterY => Cell.Y + 0.5;
    public bool IsBig => Radius >= BigRadius;

    // Kuglens midte skal være tættere på hullets midte end dette for at kunne falde (lidt mindre end det tegnede hul)
    public double FallRadius => Radius - 0.02;
}

public enum Outcome { None, Fell, Won }

// En labyrint med start, mål og huller. Samme (sværhedsgrad, seed) giver altid samme labyrint.
public class Course
{
    // Over hullet går det godt, hvis kuglen er hurtig: den falder kun ved lav fart eller hvis den bliver over hullet for længe.
    // Store huller kræver derfor næsten fuld fart og et lige tilløb på et par celler (0,88 / 0,19 s = 93 % af maks-farten).
    public const double JumpSpeed = Ball.MaxSpeed * 0.75;
    public const double MaxTimeOverHole = 0.19;   // sekunder

    // Tæt på et hul (hullets radius + PullMargin) trækkes kuglen svagt ind mod hullets midte. Trækket er svagere end
    // vippet (14), så man altid kan styre væk, og svagere end vip minus friktion ved maks-fart (4), så et hop ikke bremses.
    public const double PullMargin = 0.12;
    public const double PullAccel = 3;   // celler pr. sekund i anden

    // Mønter ligger midt i en celle. Kuglen samler en op, når de to cirkler rører hinanden.
    public const double CoinRadius = 0.12;

    public Difficulty Difficulty { get; }
    public int Seed { get; }
    public int Width { get; }
    public int Height { get; }
    public Wall[,] Walls { get; private set; } = null!;
    public Cell Start { get; private set; }
    public Cell Goal { get; private set; }
    public List<Hole> Holes { get; } = new();
    public List<Segment> Segments { get; } = new();
    public List<Cell> Coins { get; } = new();

    double timeOverHole;   // hvor længe kuglen har været inde over et hul i træk

    public Course(Difficulty difficulty, int seed)
    {
        Difficulty = difficulty;
        Seed = seed;
        var settings = DifficultySettings.For(difficulty);
        Width = settings.Width;
        Height = settings.Height;

        var random = new Random(seed);
        BuildMaze(settings, random);
        PlaceHoles(settings, random);
        PlaceCoins(settings, random);
        BuildSegments();
    }

    public static int NewSeed()
    {
        return Random.Shared.Next(1, 1000000);
    }

    // Dagens labyrint: seed er datoen (UTC) som tal, fx 20261103. Ens for alle.
    public static int DailySeed()
    {
        DateTime today = DateTime.UtcNow;
        return today.Year * 10000 + today.Month * 100 + today.Day;
    }

    // Kaldes efter hvert fysik-skridt (Ball.Step). Trækker kuglen mod et hul i nærheden. Falder den i hullet, eller er den i mål?
    // Trækket ændrer kun farten. Næste skridt i Ball klipper den til maks-farten, så serverens tjek af skridtlængden passer stadig.
    public Outcome Check(Ball ball)
    {
        bool overHole = false;
        foreach (Hole hole in Holes)
        {
            double dx = ball.X - hole.CenterX;
            double dy = ball.Y - hole.CenterY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < hole.FallRadius) overHole = true;
            if (distance > 0.001 && distance < hole.Radius + PullMargin)
            {
                ball.VX -= dx / distance * PullAccel * Ball.Step;
                ball.VY -= dy / distance * PullAccel * Ball.Step;
            }
        }

        if (overHole)
        {
            timeOverHole += Ball.Step;
            if (ball.Speed >= JumpSpeed && timeOverHole <= MaxTimeOverHole) return Outcome.None;
            timeOverHole = 0;
            return Outcome.Fell;
        }
        timeOverHole = 0;

        double gx = ball.X - (Goal.X + 0.5);
        double gy = ball.Y - (Goal.Y + 0.5);
        if (Math.Sqrt(gx * gx + gy * gy) < 0.25) return Outcome.Won;
        return Outcome.None;
    }

    // Laver labyrinten. Er den for kedelig (næsten ingen kryds), prøver vi igen med det næste tal fra samme seed.
    void BuildMaze(DifficultySettings settings, Random random)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Walls = MazeGenerator.Generate(Width, Height, random.Next(), settings.Rooms, settings.Braid);

            // Start i et tilfældigt hjørne, mål et af de fjerneste steder (bredde-først afstand)
            var corners = new[] { new Cell(0, 0), new Cell(Width - 1, 0), new Cell(0, Height - 1), new Cell(Width - 1, Height - 1) };
            Start = corners[random.Next(4)];
            int[,] distances = MazeGenerator.Distances(Walls, Start);
            int farthest = 0;
            foreach (int d in distances) farthest = Math.Max(farthest, d);

            var far = new List<Cell>();
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (distances[x, y] >= farthest * 0.9) far.Add(new Cell(x, y));
            Goal = far[random.Next(far.Count)];

            if (CrossingCount() >= Width * Height / 6 && distances[Goal.X, Goal.Y] <= Width * Height * 0.45) return;
        }
    }

    // Antal celler med 3 eller 4 åbne sider. Få kryds = én lang kedelig gang.
    int CrossingCount()
    {
        int count = 0;
        foreach (Wall cell in Walls)
            if (MazeGenerator.Openings(cell) >= 3) count++;
        return count;
    }

    // Store huller først, kun på den korteste vej, og kun hvis målet stadig kan nås uden om alle store huller
    // (der er altid en vej uden store huller, også hvis man ikke kan springe). Små huller kan ligge hvor som helst
    // (kuglen kan rulle forbi), bortset fra start og mål. Den første halvdel af de små ligger på vejen uden om de store huller.
    void PlaceHoles(DifficultySettings settings, Random random)
    {
        var used = new bool[Width, Height];
        used[Start.X, Start.Y] = true;
        used[Goal.X, Goal.Y] = true;
        var blocked = new bool[Width, Height];   // kun store huller

        int big = 0;
        for (int tries = 0; tries < 300 && big < settings.BigHoles; tries++)
        {
            var cell = new Cell(random.Next(Width), random.Next(Height));
            if (used[cell.X, cell.Y] || MazeGenerator.Openings(Walls[cell.X, cell.Y]) < 2) continue;
            if (!IsOnShortestPath(cell, blocked)) continue;

            blocked[cell.X, cell.Y] = true;
            if (MazeGenerator.Distances(Walls, Start, blocked)[Goal.X, Goal.Y] < 0)
            {
                blocked[cell.X, cell.Y] = false;   // ville spærre vejen, så ikke her
                continue;
            }
            used[cell.X, cell.Y] = true;
            Holes.Add(new Hole(cell, Hole.BigRadius));
            big++;
        }

        int small = 0;
        for (int tries = 0; tries < 300 && small < settings.SmallHoles; tries++)
        {
            var cell = new Cell(random.Next(Width), random.Next(Height));
            if (used[cell.X, cell.Y]) continue;
            bool onRoute = small < settings.SmallHoles / 2 && tries < 200;   // finder vi ingen, må de ligge hvor som helst
            if (onRoute && !IsOnShortestPath(cell, blocked)) continue;

            used[cell.X, cell.Y] = true;
            double radius = settings.SmallMin + random.NextDouble() * (settings.SmallMax - settings.SmallMin);
            Holes.Add(new Hole(cell, radius));
            small++;
        }
    }

    // Ligger cellen på en korteste vej fra start til mål (uden om de store huller, der er sat indtil nu)?
    bool IsOnShortestPath(Cell cell, bool[,] blocked)
    {
        int[,] fromStart = MazeGenerator.Distances(Walls, Start, blocked);
        int[,] fromGoal = MazeGenerator.Distances(Walls, Goal, blocked);
        int d = fromStart[cell.X, cell.Y];
        return d >= 0 && d + fromGoal[cell.X, cell.Y] == fromStart[Goal.X, Goal.Y];
    }

    // Rører kuglen mønt nummer i? Mønten påvirker hverken fysik, tid eller spøgelse.
    public bool TouchesCoin(Ball ball, int i)
    {
        double dx = ball.X - (Coins[i].X + 0.5);
        double dy = ball.Y - (Coins[i].Y + 0.5);
        double reach = Ball.Radius + CoinRadius;
        return dx * dx + dy * dy < reach * reach;
    }

    // Mønter i celler uden start, mål og huller. Celler uden for den korteste vej kommer først,
    // så det koster tid at samle dem. Samme (sværhedsgrad, seed) giver altid de samme mønter.
    void PlaceCoins(DifficultySettings settings, Random random)
    {
        var taken = new bool[Width, Height];
        taken[Start.X, Start.Y] = true;
        taken[Goal.X, Goal.Y] = true;
        foreach (Hole hole in Holes)
            taken[hole.Cell.X, hole.Cell.Y] = true;

        var offPath = new List<Cell>();
        var onPath = new List<Cell>();
        var noBlocks = new bool[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (taken[x, y]) continue;
                var cell = new Cell(x, y);
                if (IsOnShortestPath(cell, noBlocks)) onPath.Add(cell);
                else offPath.Add(cell);
            }
        }

        // Trækker tilfældigt: først blandt cellerne uden for vejen, derefter blandt resten
        while (Coins.Count < settings.Coins && (offPath.Count > 0 || onPath.Count > 0))
        {
            List<Cell> from = offPath.Count > 0 ? offPath : onPath;
            int i = random.Next(from.Count);
            Coins.Add(from[i]);
            from.RemoveAt(i);
        }
    }

    // Laver alle vægge om til linjestykker. Hver væg tegnes kun én gang:
    // nord og vest for alle celler, plus øst for sidste kolonne og syd for sidste række.
    void BuildSegments()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Wall w = Walls[x, y];
                if (w.HasFlag(Wall.North)) Segments.Add(new Segment(x, y, x + 1, y));
                if (w.HasFlag(Wall.West)) Segments.Add(new Segment(x, y, x, y + 1));
                if (x == Width - 1 && w.HasFlag(Wall.East)) Segments.Add(new Segment(x + 1, y, x + 1, y + 1));
                if (y == Height - 1 && w.HasFlag(Wall.South)) Segments.Add(new Segment(x, y + 1, x + 1, y + 1));
            }
        }
    }
}
