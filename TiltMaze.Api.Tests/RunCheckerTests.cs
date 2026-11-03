using TiltMaze.Core;

namespace TiltMaze.Api.Tests;

public class RunCheckerTests
{
    // Computer-spilleren kører uden om de store huller og forbi de små, på alle sværhedsgrader
    [Theory]
    [InlineData(Difficulty.Easy, 3, 2.0)]
    [InlineData(Difficulty.Easy, 3, 3.0)]
    [InlineData(Difficulty.Easy, 19, 2.0)]
    [InlineData(Difficulty.Normal, 5, 2.0)]
    [InlineData(Difficulty.Normal, 12, 3.0)]
    [InlineData(Difficulty.Hard, 7, 3.0)]
    [InlineData(Difficulty.Hard, 30, 2.0)]
    [InlineData(Difficulty.Expert, 9, 2.0)]
    [InlineData(Difficulty.Expert, 40, 3.0)]
    public void GenuineRun_IsAccepted(Difficulty difficulty, int seed, double speed)
    {
        var course = new Course(difficulty, seed);
        var player = Player.Play(course, 120, speed);

        Assert.True(player.Won);
        Assert.Null(RunChecker.Check(course, player.TimeMs, player.Samples));
    }

    // Kører lige gennem hullerne med fuld fart: hoppet over et hul er en lovlig tur
    [Theory]
    [InlineData(20)]
    [InlineData(64)]
    public void RunThatJumpsOverHoles_IsAccepted(int seed)
    {
        var course = new Course(Difficulty.Easy, seed);
        var player = Player.Play(course, 60, 3, null, avoidHoles: false);

        Assert.True(player.Won);
        Assert.Equal(0, player.Falls);
        bool overHole = false;
        for (int i = 0; i < player.Samples.Length; i += 2)
            foreach (Hole hole in course.Holes)
            {
                double dx = player.Samples[i] - hole.CenterX, dy = player.Samples[i + 1] - hole.CenterY;
                if (Math.Sqrt(dx * dx + dy * dy) < hole.FallRadius)
                    overHole = true;
            }
        Assert.True(overHole);
        Assert.Null(RunChecker.Check(course, player.TimeMs, player.Samples));
    }

    // Skiftevis hurtige (10 ms) og langsomme (70 ms) billeder: optagelsen følger fysikken, så turen er stadig i orden
    [Theory]
    [InlineData(3)]
    [InlineData(19)]
    public void IrregularFrameTimes_AreAccepted(int seed)
    {
        var course = new Course(Difficulty.Easy, seed);
        var player = Player.Play(course, 60, 2, [0.010, 0.070]);

        Assert.True(player.Won);
        Assert.Null(RunChecker.Check(course, player.TimeMs, player.Samples));
    }

    [Fact]
    public void Teleport_IsRejected()
    {
        var course = new Course(Difficulty.Easy, 3);
        var genuine = Player.Play(course);
        // Start og mål, og intet imellem (tiden passer med antal målinger)
        var ghost = new float[] { genuine.Samples[0], genuine.Samples[1], genuine.Samples[^2], genuine.Samples[^1] };

        Assert.Equal("moved too far between two points", RunChecker.Check(course, 33, ghost));
    }

    [Fact]
    public void TeleportInTheMiddle_IsRejected()
    {
        var course = new Course(Difficulty.Easy, 3);
        var genuine = Player.Play(course);
        // Hopper fra midten af turen direkte til slutningen
        int middle = genuine.Samples.Length / 4 * 2;
        var ghost = genuine.Samples.Take(middle).Concat(genuine.Samples.TakeLast(2)).ToArray();
        int timeMs = (ghost.Length / 2 - 1) * 1000 / 30;

        Assert.Equal("moved too far between two points", RunChecker.Check(course, timeMs, ghost));
    }

    [Fact]
    public void WalkingThroughWalls_IsRejected()
    {
        var course = new Course(Difficulty.Easy, 3);
        // Lige linje fra start til mål i lovlige skridt på 0,1, men gennem væggene
        double startX = course.Start.X + 0.5, startY = course.Start.Y + 0.5;
        double goalX = course.Goal.X + 0.5, goalY = course.Goal.Y + 0.5;
        double length = Math.Sqrt((goalX - startX) * (goalX - startX) + (goalY - startY) * (goalY - startY));
        int steps = (int)Math.Ceiling(length / 0.1);
        var ghost = new List<float>();
        for (int i = 0; i <= steps; i++)
        {
            ghost.Add((float)(startX + (goalX - startX) * i / steps));
            ghost.Add((float)(startY + (goalY - startY) * i / steps));
        }
        int timeMs = steps * 1000 / 30;

        Assert.Equal("inside a wall", RunChecker.Check(course, timeMs, ghost.ToArray()));
    }

    [Fact]
    public void TooFast_IsRejected()
    {
        var course = new Course(Difficulty.Easy, 3);
        var genuine = Player.Play(course, 60, 2);
        // Hver 4. måling af en rigtig tur: samme vej, men 4 gange så hurtigt (over maks-fart)
        var ghost = new List<float>();
        int count = genuine.Samples.Length / 2;
        for (int i = 0; i < count; i += 4)
        {
            ghost.Add(genuine.Samples[i * 2]);
            ghost.Add(genuine.Samples[i * 2 + 1]);
        }
        ghost.Add(genuine.Samples[^2]);
        ghost.Add(genuine.Samples[^1]);
        int timeMs = (ghost.Count / 2 - 1) * 1000 / 30;

        Assert.Equal("faster than the maximum speed", RunChecker.Check(course, timeMs, ghost.ToArray()));
    }

    [Fact]
    public void WrongTime_IsRejected()
    {
        var course = new Course(Difficulty.Easy, 3);
        var genuine = Player.Play(course);

        Assert.Equal("time does not match ghost length", RunChecker.Check(course, genuine.TimeMs / 2, genuine.Samples));
    }

    [Fact]
    public void RunThatStopsBeforeTheGoal_IsRejected()
    {
        var course = new Course(Difficulty.Easy, 3);
        var genuine = Player.Play(course);
        var ghost = genuine.Samples.Take(genuine.Samples.Length / 4 * 2).ToArray();
        int timeMs = (ghost.Length / 2 - 1) * 1000 / 30;

        Assert.Equal("does not end in the goal", RunChecker.Check(course, timeMs, ghost));
    }

    [Fact]
    public void RunThatDoesNotStartAtTheStart_IsRejected()
    {
        var course = new Course(Difficulty.Easy, 3);
        var genuine = Player.Play(course);
        var ghost = genuine.Samples.Skip(20).ToArray();
        int timeMs = (ghost.Length / 2 - 1) * 1000 / 30;

        Assert.Equal("does not start at the start", RunChecker.Check(course, timeMs, ghost));
    }

    [Fact]
    public void BadNumbers_AreRejected()
    {
        var course = new Course(Difficulty.Easy, 3);

        Assert.NotNull(RunChecker.Check(course, 100, []));
        Assert.NotNull(RunChecker.Check(course, 100, [1f, 2f, 3f]));
        Assert.NotNull(RunChecker.Check(course, 100, [float.NaN, 0f, 0f, 0f]));
        Assert.NotNull(RunChecker.Check(course, 0, [0f, 0f, 0f, 0f]));
    }

    // Easy 1 lige gennem hullerne falder igen og igen. Faldene (hop tilbage til start) skal tælle som lovlige,
    // så det eneste der er galt ved denne tur er, at den ikke når målet
    [Fact]
    public void Falls_AreAccepted()
    {
        var course = new Course(Difficulty.Easy, 1);
        var player = Player.Play(course, 10, 2, null, avoidHoles: false);

        Assert.True(player.Falls > 0);
        int timeMs = (player.Samples.Length / 2 - 1) * 1000 / 30;
        Assert.Equal("does not end in the goal", RunChecker.Check(course, timeMs, player.Samples));
    }

    // Kuglen bliver i hullet uden at falde tilbage til start: kan ikke lade sig gøre
    [Fact]
    public void StayingInAHole_IsRejected()
    {
        var course = new Course(Difficulty.Easy, 1);
        var player = Player.Play(course, 10, 2, null, avoidHoles: false);
        var samples = player.Samples;

        // Find den første måling ved et hul, rul langsomt ind til midten, og lad kuglen blive der resten af turen
        int near = -1;
        Hole target = default;
        for (int i = 1; i < samples.Length / 2 && near < 0; i++)
            foreach (Hole hole in course.Holes)
            {
                double dx = samples[i * 2] - hole.CenterX, dy = samples[i * 2 + 1] - hole.CenterY;
                if (Math.Sqrt(dx * dx + dy * dy) < hole.Radius)
                {
                    near = i;
                    target = hole;
                }
            }
        Assert.True(near > 0);

        var ghost = samples.Take(near * 2).ToList();
        float x = samples[near * 2], y = samples[near * 2 + 1];
        for (int i = 0; i <= 5; i++)
        {
            ghost.Add((float)(x + (target.CenterX - x) * i / 5));
            ghost.Add((float)(y + (target.CenterY - y) * i / 5));
        }
        for (int i = 0; i < 20; i++)
        {
            ghost.Add((float)target.CenterX);
            ghost.Add((float)target.CenterY);
        }
        int timeMs = (ghost.Count / 2 - 1) * 1000 / 30;

        Assert.Contains(RunChecker.Check(course, timeMs, ghost.ToArray()), new[] { "stopped in a hole", "too long over a hole" });
    }
}
