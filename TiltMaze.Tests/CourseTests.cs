using TiltMaze.Core;

namespace TiltMaze.Tests;

public class CourseTests
{
    const int Seeds = 150;

    static readonly Difficulty[] All = { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard, Difficulty.Expert };

    // Hver test kører for alle sværhedsgrader og mange seeds
    public static IEnumerable<object[]> Difficulties => All.Select(d => new object[] { d });

    [Theory]
    [MemberData(nameof(Difficulties))]
    public void EveryCellCanBeReachedFromStart(Difficulty difficulty)
    {
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var course = new Course(difficulty, seed);
            foreach (int d in MazeGenerator.Distances(course.Walls, course.Start))
                Assert.True(d >= 0, $"{difficulty} {seed}: a cell cannot be reached");
        }
    }

    [Theory]
    [MemberData(nameof(Difficulties))]
    public void GoalCanBeReachedWithBigHolesBlocked(Difficulty difficulty)
    {
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var course = new Course(difficulty, seed);
            var blocked = new bool[course.Width, course.Height];
            foreach (Hole hole in course.Holes.Where(h => h.IsBig))
                blocked[hole.Cell.X, hole.Cell.Y] = true;
            int[,] distances = MazeGenerator.Distances(course.Walls, course.Start, blocked);
            Assert.True(distances[course.Goal.X, course.Goal.Y] > 0, $"{difficulty} {seed}: goal is cut off");
        }
    }

    [Theory]
    [MemberData(nameof(Difficulties))]
    public void StartAndGoalAreFarApartAndNeverHoles(Difficulty difficulty)
    {
        var settings = DifficultySettings.For(difficulty);
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var course = new Course(difficulty, seed);
            bool corner = (course.Start.X == 0 || course.Start.X == course.Width - 1) && (course.Start.Y == 0 || course.Start.Y == course.Height - 1);
            Assert.True(corner, "Start is a corner");
            Assert.DoesNotContain(course.Holes, h => h.Cell == course.Start || h.Cell == course.Goal);
            Assert.Equal(course.Holes.Count, course.Holes.Select(h => h.Cell).Distinct().Count());

            int distance = MazeGenerator.Distances(course.Walls, course.Start)[course.Goal.X, course.Goal.Y];
            Assert.True(distance >= (course.Width + course.Height) * 3 / 4, $"{difficulty} {seed}: goal is too close ({distance})");
            Assert.True(course.Holes.Count(h => h.IsBig) <= settings.BigHoles, $"{difficulty} {seed}: too many big holes");
            Assert.True(course.Holes.Count(h => !h.IsBig) == settings.SmallHoles, $"{difficulty} {seed}: wrong number of small holes");
        }
    }

    [Theory]
    [MemberData(nameof(Difficulties))]
    public void SameSeedGivesSameCourse(Difficulty difficulty)
    {
        for (int seed = 1; seed <= 20; seed++)
        {
            var a = new Course(difficulty, seed);
            var b = new Course(difficulty, seed);
            Assert.Equal(a.Walls.Cast<Wall>(), b.Walls.Cast<Wall>());
            Assert.Equal(a.Holes, b.Holes);
            Assert.Equal(a.Start, b.Start);
            Assert.Equal(a.Goal, b.Goal);
        }
    }

    [Fact]
    public void SmallHolesLeaveAGapAlongTheWallAndBigHolesFillTheCorridor()
    {
        // Længst ind mod væggen kan kuglens midte komme 0,2 fra gangens midte (gang 1,0 minus væg minus kugle)
        double reach = 0.5 - Ball.WallHalfWidth - Ball.Radius;
        foreach (Difficulty difficulty in All)
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                foreach (Hole hole in new Course(difficulty, seed).Holes)
                {
                    if (hole.IsBig)
                        Assert.True(hole.FallRadius > Math.Sqrt(2) * reach, "A big hole must cover the whole corridor");
                    else
                        Assert.True(reach - hole.FallRadius >= 0.04, $"A small hole of radius {hole.Radius} leaves no gap");
                }
            }
        }
    }

    [Fact]
    public void BallFallsInHoleAtLowSpeedButJumpsAtHighSpeed()
    {
        var course = new Course(Difficulty.Hard, 1);
        Hole big = course.Holes.First(h => h.IsBig);
        Hole small = course.Holes.First(h => !h.IsBig);

        // Fuld fart og langt tilløb: over begge. Lige fra start midt i hullet, eller kort tilløb: ned.
        Assert.Equal(Outcome.None, Roll(course, big, 1.5, Ball.MaxSpeed));
        Assert.Equal(Outcome.None, Roll(course, small, 1.5, Ball.MaxSpeed));
        Assert.Equal(Outcome.Fell, Roll(course, big, 0.6, 0));
        Assert.Equal(Outcome.Fell, Roll(course, small, 0, 0));
    }

    [Fact]
    public void BallRollsPastSmallHoleBesideIt()
    {
        var course = new Course(Difficulty.Normal, 1);
        Hole small = course.Holes.First(h => !h.IsBig);
        // Fra hvile med fuld vip, så langt fra hullets midte som væggen tillader: farten er lav ved hullet, men kuglen er ved siden af
        double beside = 0.5 - Ball.WallHalfWidth - Ball.Radius;
        Assert.Equal(Outcome.None, Roll(course, small, 0.8, 0, beside));
    }

    // Ruller en kugle uden vægge mod et hul (fuld vip mod øst), fra "distance" før hullets midte, med startfart og
    // afstand til siden fra midten. Giver første udfald.
    static Outcome Roll(Course course, Hole hole, double distance, double startSpeed, double offset = 0)
    {
        var ball = new Ball(new List<Segment>(), hole.CenterX - distance, hole.CenterY + offset);
        ball.VX = startSpeed;
        Outcome result = Outcome.None;
        ball.Update(1.0, 1, 0, () =>
        {
            result = course.Check(ball);
            return result != Outcome.None;
        });
        return result;
    }
}
