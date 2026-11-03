using TiltMaze.Core;

namespace TiltMaze.Api.Tests;

// En enkel computer-spiller, der spiller en labyrint på samme måde som Game.razor.cs
// (billeder på 16 ms, spøgelset optages hvert 1/30 s, hul = tilbage til start).
// Den følger korteste vej mellem cellernes midter. Bruges til at lave rigtige ture til testene.
public class Player
{
    public bool Won;
    public int TimeMs;
    public int Falls;
    public float[] Samples = [];

    public static Player Play(Course course, double maxSeconds = 120, double speed = 2)
    {
        var path = ShortestPath(course);
        var ball = new Ball(course.Segments, course.Start.X + 0.5, course.Start.Y + 0.5);
        var recording = new List<float>();
        var player = new Player();
        double time = 0;
        int next = 1;   // næste celle på vejen
        var outcome = Outcome.None;

        while (time < maxSeconds && outcome != Outcome.Won)
        {
            // Mod næste celles midte. Står der et hul på vejen, løber vi på det med fuld fart
            Cell target = path[next];
            double dx = target.X + 0.5 - ball.X;
            double dy = target.Y + 0.5 - ball.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < 0.2 && next < path.Count - 1)
            {
                next++;
                continue;
            }
            bool hole = course.Holes.Any(h => h.Cell == target || (next + 1 < path.Count && h.Cell == path[next + 1]));
            double wanted = hole ? 4.5 : speed;
            double ax = Math.Clamp((dx / distance * wanted - ball.VX) * 0.5, -1, 1);
            double ay = Math.Clamp((dy / distance * wanted - ball.VY) * 0.5, -1, 1);

            outcome = Outcome.None;
            ball.Update(0.016, ax, ay, () =>
            {
                outcome = course.Check(ball);
                return outcome != Outcome.None;
            });
            time += 0.016;

            // Som Game.Record()
            while (recording.Count < ((int)(time * Ghost.Rate) + 1) * 2)
            {
                recording.Add((float)ball.X);
                recording.Add((float)ball.Y);
            }

            if (outcome == Outcome.Fell)
            {
                player.Falls++;
                ball.Reset(course.Start.X + 0.5, course.Start.Y + 0.5);
                next = 1;
            }
        }

        player.Won = outcome == Outcome.Won;
        player.TimeMs = (int)Math.Round(time * 1000);
        player.Samples = recording.ToArray();
        return player;
    }

    // Bredde-først fra start til mål gennem gange uden væg
    static List<Cell> ShortestPath(Course course)
    {
        var from = new Dictionary<Cell, Cell>();
        var queue = new Queue<Cell>();
        queue.Enqueue(course.Start);
        from[course.Start] = course.Start;

        while (queue.Count > 0)
        {
            Cell cell = queue.Dequeue();
            Wall wall = course.Walls[cell.X, cell.Y];
            var neighbours = new List<Cell>();
            if (!wall.HasFlag(Wall.North)) neighbours.Add(new Cell(cell.X, cell.Y - 1));
            if (!wall.HasFlag(Wall.South)) neighbours.Add(new Cell(cell.X, cell.Y + 1));
            if (!wall.HasFlag(Wall.West)) neighbours.Add(new Cell(cell.X - 1, cell.Y));
            if (!wall.HasFlag(Wall.East)) neighbours.Add(new Cell(cell.X + 1, cell.Y));
            foreach (Cell n in neighbours)
            {
                if (n.X < 0 || n.Y < 0 || n.X >= course.Width || n.Y >= course.Height || from.ContainsKey(n)) continue;
                from[n] = cell;
                queue.Enqueue(n);
            }
        }

        var path = new List<Cell>();
        for (Cell c = course.Goal; c != course.Start; c = from[c])
            path.Add(c);
        path.Add(course.Start);
        path.Reverse();
        return path;
    }
}
