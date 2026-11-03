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
    public void FormatTimeRoundsToTenthsFirst()
    {
        Assert.Equal("1:15.3", Format.Time(75.3));
        Assert.Equal("0:00.0", Format.Time(0));
        Assert.Equal("0:59.9", Format.Time(59.94));
        Assert.Equal("1:00.0", Format.Time(59.96));
    }
}
