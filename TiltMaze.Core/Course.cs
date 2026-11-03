namespace TiltMaze.Core;

// Et linjestykke (en væg) i banens koordinater. 1 enhed = 1 celle.
public record struct Segment(double X1, double Y1, double X2, double Y2);

// En bane: labyrint, start, mål og huller. Bane n har seed n, så den er altid ens.
public class Level
{
    public int Number { get; }
    public int Width { get; }
    public int Height { get; }
    public Wall[,] Walls { get; }
    public Cell Start { get; }
    public Cell Goal { get; }
    public List<Cell> Holes { get; } = new();
    public List<Segment> Segments { get; } = new();

    public Level(int number)
    {
        Number = number;

        // Størrelsen vokser: bane 1 = 5x7, bane 12 = 10x14
        Width = 5 + (number - 1) * 5 / 11;
        Height = 7 + (number - 1) * 7 / 11;
        Walls = MazeGenerator.Generate(Width, Height, number, number / 4, 0.3);
        Start = new Cell(0, 0);
        Goal = new Cell(Width - 1, Height - 1);

        PlaceHoles();
        BuildSegments();
    }

    // Placerer huller på tilfældige celler (seed + 1000). Et hul må ikke være start eller mål,
    // og målet skal stadig kunne nås fra start uden om hullerne.
    void PlaceHoles()
    {
        var random = new Random(Number + 1000);
        int wanted = 1 + Number / 2;
        var blocked = new bool[Width, Height];

        for (int tries = 0; tries < 200 && Holes.Count < wanted; tries++)
        {
            var cell = new Cell(random.Next(Width), random.Next(Height));
            if (cell == Start || cell == Goal || blocked[cell.X, cell.Y]) continue;

            blocked[cell.X, cell.Y] = true;
            int[,] distances = MazeGenerator.Distances(Walls, Start, blocked);
            if (distances[Goal.X, Goal.Y] < 0)
                blocked[cell.X, cell.Y] = false;   // ville spærre vejen, så ikke her
            else
                Holes.Add(cell);
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
