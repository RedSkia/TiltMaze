using TiltMaze.Core;

namespace TiltMaze.Tests;

public class GhostTests
{
    // Tre målinger med 10 pr. sekund: (0.5, 0.5), (1.5, 0.5), (1.5, 1.5)
    static Ghost Make()
    {
        float[] samples = { 0.5f, 0.5f, 1.5f, 0.5f, 1.5f, 1.5f };
        return new Ghost(samples, 10, 3, 3);
    }

    [Fact]
    public void PositionIsBetweenTwoSamples()
    {
        Assert.True(Make().TryGetPosition(0.05, out double x, out double y));
        Assert.Equal(1.0, x, 6);
        Assert.Equal(0.5, y, 6);
    }

    [Fact]
    public void RecordingEnds()
    {
        Assert.False(Make().TryGetPosition(0.25, out _, out _));
    }

    [Fact]
    public void FirstVisitGivesTheTimeTheGhostEnteredTheCell()
    {
        Ghost ghost = Make();
        Assert.Equal(0.0, ghost.FirstVisit(0, 0));
        Assert.Equal(0.1, ghost.FirstVisit(1, 0)!.Value, 6);
        Assert.Equal(0.2, ghost.FirstVisit(1, 1)!.Value, 6);
        Assert.Null(ghost.FirstVisit(2, 2));
        Assert.Null(ghost.FirstVisit(5, 5));
    }
}
