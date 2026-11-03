namespace TiltMaze.Core;

// Hvilke vægge en celle har. Flere kan kombineres, fx Wall.North | Wall.East.
[Flags]
public enum Wall { None = 0, North = 1, East = 2, South = 4, West = 8, All = 15 }

// En celle i labyrinten. X = kolonne (mod højre), Y = række (nedad, række 0 er øverst).
public record struct Cell(int X, int Y);

// Labyrint-matematik uden grafik: DFS-labyrint + små rum + "braiding" (løkker) og bredde-først afstande.
// Samme seed giver altid samme labyrint.
public static class MazeGenerator
{
    // De 4 retninger i rækkefølgen Nord, Øst, Syd, Vest. Retning d + 2 er altid modsat side.
    static readonly int[] StepX = { 0, 1, 0, -1 };
    static readonly int[] StepY = { -1, 0, 1, 0 };
    static readonly Wall[] Walls = { Wall.North, Wall.East, Wall.South, Wall.West };

    // Recursive backtracker: gå til en tilfældig nabo, der ikke er besøgt, og fjern væggen imellem.
    // Ved en blindgyde går vi tilbage, til en celle igen har ubesøgte naboer. Alle celler kan nås.
    public static Wall[,] Generate(int width, int height, int seed, int roomCount, double braidChance)
    {
        var grid = new Wall[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = Wall.All;

        var random = new Random(seed);   // samme seed = samme labyrint
        var visited = new bool[width, height];
        var path = new Stack<Cell>();
        var options = new List<int>();

        visited[0, 0] = true;
        path.Push(new Cell(0, 0));

        while (path.Count > 0)
        {
            Cell cell = path.Peek();

            options.Clear();
            for (int d = 0; d < 4; d++)
            {
                int nx = cell.X + StepX[d];
                int ny = cell.Y + StepY[d];
                if (Inside(grid, nx, ny) && !visited[nx, ny]) options.Add(d);
            }

            if (options.Count == 0)
            {
                path.Pop();
                continue;
            }

            int direction = options[random.Next(options.Count)];
            Open(grid, cell.X, cell.Y, direction);
            var next = new Cell(cell.X + StepX[direction], cell.Y + StepY[direction]);
            visited[next.X, next.Y] = true;
            path.Push(next);
        }

        CarveRooms(grid, random, roomCount);
        Braid(grid, random, braidChance);
        return grid;
    }

    // Antal åbne sider på en celle (1 = blindgyde, 2 = gang, 3+ = kryds).
    public static int Openings(Wall cell)
    {
        int count = 0;
        foreach (Wall wall in Walls)
            if (!cell.HasFlag(wall)) count++;
        return count;
    }

    // Antal skridt fra start til hver celle, kun gennem åbne sider (bredde-først). -1 = kan ikke nås.
    // Celler i "blocked" (fx huller) må ikke bruges. blocked kan være null.
    public static int[,] Distances(Wall[,] grid, Cell start, bool[,]? blocked = null)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        var distances = new int[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                distances[x, y] = -1;

        var queue = new Queue<Cell>();
        distances[start.X, start.Y] = 0;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Cell cell = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int nx = cell.X + StepX[d];
                int ny = cell.Y + StepY[d];
                if (grid[cell.X, cell.Y].HasFlag(Walls[d]) || !Inside(grid, nx, ny) || distances[nx, ny] >= 0) continue;
                if (blocked != null && blocked[nx, ny]) continue;

                distances[nx, ny] = distances[cell.X, cell.Y] + 1;
                queue.Enqueue(new Cell(nx, ny));
            }
        }
        return distances;
    }

    static bool Inside(Wall[,] grid, int x, int y)
    {
        return x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1);
    }

    // Fjerner væggen mellem en celle og dens nabo i retning d (fra begge celler).
    static void Open(Wall[,] grid, int x, int y, int d)
    {
        grid[x, y] &= ~Walls[d];
        grid[x + StepX[d], y + StepY[d]] &= ~Walls[(d + 2) % 4];
    }

    // Fjerner de indre vægge i tilfældige 2x2-blokke. Fjerner kun vægge, så alt hænger stadig sammen.
    static void CarveRooms(Wall[,] grid, Random random, int roomCount)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        if (width < 2 || height < 2) return;

        for (int i = 0; i < roomCount; i++)
        {
            int x = random.Next(width - 1);
            int y = random.Next(height - 1);
            Open(grid, x, y, 1);       // øst for øverst-venstre
            Open(grid, x, y + 1, 1);   // øst for nederst-venstre
            Open(grid, x, y, 2);       // syd for øverst-venstre
            Open(grid, x + 1, y, 2);   // syd for øverst-højre
        }
    }

    // Åbner en ekstra væg i nogle af blindgyderne, så labyrinten får løkker.
    static void Braid(Wall[,] grid, Random random, double braidChance)
    {
        var closed = new List<int>();
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                if (Openings(grid[x, y]) != 1 || random.NextDouble() >= braidChance) continue;

                closed.Clear();
                for (int d = 0; d < 4; d++)
                {
                    if (grid[x, y].HasFlag(Walls[d]) && Inside(grid, x + StepX[d], y + StepY[d])) closed.Add(d);
                }
                if (closed.Count > 0) Open(grid, x, y, closed[random.Next(closed.Count)]);
            }
        }
    }
}
