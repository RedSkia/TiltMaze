using System.Text.Json;
using TiltMaze.Core;

namespace TiltMaze.Tests;

// Facit til TypeScript-udgaven af labyrinten (supabase/functions/_shared/course.ts).
// Testen sammenligner med filen course.json. Er labyrinten ændret med vilje: kør med WRITE_FIXTURES=1 for at skrive filen igen.
public class CourseFixtureTests
{
    [Fact]
    public void CourseMatchesFixtureUsedByTypeScript()
    {
        var courses = new List<object>();
        foreach (Difficulty difficulty in Enum.GetValues<Difficulty>())
        {
            foreach (int seed in new[] { 3, 20, 20261103 })
            {
                var course = new Course(difficulty, seed);

                // Sum af alle vægkoordinater: ændres én væg, ændres summen
                double wallSum = 0;
                foreach (Segment wall in course.Segments)
                    wallSum += wall.X1 + wall.Y1 + wall.X2 + wall.Y2;

                courses.Add(new
                {
                    difficulty = (int)difficulty,
                    seed,
                    start = new[] { course.Start.X, course.Start.Y },
                    goal = new[] { course.Goal.X, course.Goal.Y },
                    walls = course.Segments.Count,
                    wallSum,
                    holes = course.Holes.Select(h => new[] { h.Cell.X, h.Cell.Y, h.Radius }).ToList(),
                });
            }
        }

        string json = JsonSerializer.Serialize(courses, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n";
        string path = Path.Combine(FindRoot(), "supabase", "functions", "_shared", "fixtures", "course.json");
        if (Environment.GetEnvironmentVariable("WRITE_FIXTURES") == "1")
            File.WriteAllText(path, json);

        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), json);
    }

    static string FindRoot()
    {
        string? folder = AppContext.BaseDirectory;
        while (folder != null && !File.Exists(Path.Combine(folder, "TiltMaze.slnx")))
            folder = Path.GetDirectoryName(folder);
        return folder ?? throw new InvalidOperationException("Root not found");
    }
}
