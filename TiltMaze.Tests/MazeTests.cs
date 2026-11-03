using TiltMaze.Core;

namespace TiltMaze.Tests;

public class MazeTests
{
    [Fact]
    public void SameSeedGivesSameMaze()
    {
        var a = MazeGenerator.Generate(8, 11, 42, 2, 0.3);
        var b = MazeGenerator.Generate(8, 11, 42, 2, 0.3);

        Assert.Equal(a.Cast<Wall>(), b.Cast<Wall>());
    }

    [Fact]
    public void DifferentSeedGivesDifferentMaze()
    {
        var a = MazeGenerator.Generate(8, 11, 1, 2, 0.3);
        var b = MazeGenerator.Generate(8, 11, 2, 2, 0.3);

        Assert.NotEqual(a.Cast<Wall>(), b.Cast<Wall>());
    }

    [Fact]
    public void EveryCellCanBeReachedFromStart()
    {
        for (int seed = 1; seed <= 12; seed++)
        {
            var level = new Level(seed);
            int[,] distances = MazeGenerator.Distances(level.Walls, level.Start);

            foreach (int d in distances)
                Assert.True(d >= 0, $"Level {seed} has a cell that cannot be reached");
        }
    }

    [Fact]
    public void GoalCanBeReachedWithoutTouchingHoles()
    {
        for (int number = 1; number <= 12; number++)
        {
            var level = new Level(number);
            var blocked = new bool[level.Width, level.Height];
            foreach (Cell hole in level.Holes)
                blocked[hole.X, hole.Y] = true;

            int[,] distances = MazeGenerator.Distances(level.Walls, level.Start, blocked);

            Assert.True(distances[level.Goal.X, level.Goal.Y] > 0, $"Level {number}: goal is cut off");
        }
    }

    [Fact]
    public void HolesAreNeverOnStartOrGoal()
    {
        for (int number = 1; number <= 12; number++)
        {
            var level = new Level(number);

            Assert.NotEmpty(level.Holes);
            Assert.DoesNotContain(level.Start, level.Holes);
            Assert.DoesNotContain(level.Goal, level.Holes);
        }
    }

    [Fact]
    public void LevelsGrowWithTheNumber()
    {
        var first = new Level(1);
        var last = new Level(12);

        Assert.Equal(5, first.Width);
        Assert.Equal(7, first.Height);
        Assert.Equal(10, last.Width);
        Assert.Equal(14, last.Height);
    }
}
