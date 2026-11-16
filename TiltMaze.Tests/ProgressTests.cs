using TiltMaze.Core;

namespace TiltMaze.Tests;

public class ProgressTests
{
    private static int ShortestPath(Course course)
    {
        return MazeGenerator.Distances(course.Walls, course.Start)[course.Goal.X, course.Goal.Y];
    }

    [Fact]
    public void StarsFollowTimePerCell()
    {
        var course = new Course(Difficulty.Normal, 1);
        int cells = ShortestPath(course);
        Assert.Equal(3, Rewards.Stars(course, cells * Rewards.ThreeStarPerCell));
        Assert.Equal(2, Rewards.Stars(course, cells * Rewards.ThreeStarPerCell + 0.1));
        Assert.Equal(2, Rewards.Stars(course, cells * Rewards.TwoStarPerCell));
        Assert.Equal(1, Rewards.Stars(course, cells * Rewards.TwoStarPerCell + 0.1));
    }

    [Fact]
    public void ThreeStarsNeedsLessThanMaxSpeed()
    {
        // Maks-farten er 5 celler/s (0,2 s pr. celle), så 0,5 s pr. celle er mulig
        Assert.True(Rewards.ThreeStarPerCell > 1 / Ball.MaxSpeed);
    }

    [Fact]
    public void StreakGrowsDayAfterDay()
    {
        var day = new DateOnly(2026, 11, 16);
        Assert.Equal(1, Rewards.NextStreak(null, day, 0));
        Assert.Equal(4, Rewards.NextStreak(day.AddDays(-1), day, 3));
        Assert.Equal(3, Rewards.NextStreak(day, day, 3));
        Assert.Equal(1, Rewards.NextStreak(day.AddDays(-2), day, 9));
    }

    [Fact]
    public void ShownStreakIsZeroAfterAMissedDay()
    {
        var day = new DateOnly(2026, 11, 16);
        Assert.Equal(5, Rewards.ShownStreak(day, day, 5));
        Assert.Equal(5, Rewards.ShownStreak(day.AddDays(-1), day, 5));
        Assert.Equal(0, Rewards.ShownStreak(day.AddDays(-2), day, 5));
        Assert.Equal(0, Rewards.ShownStreak(null, day, 0));
    }

    [Fact]
    public void AchievementsAreEarnedByTheirRules()
    {
        Assert.Equal(["first"], Rewards.Achieved(Difficulty.Easy, 1, 0, 1).Select(a => a.Id));
        Assert.Contains(Rewards.Achieved(Difficulty.Expert, 1, 0, 1), a => a.Id == "expert");
        Assert.Contains(Rewards.Achieved(Difficulty.Hard, 3, 0, 1), a => a.Id == "hard3");
        Assert.DoesNotContain(Rewards.Achieved(Difficulty.Hard, 2, 0, 1), a => a.Id == "hard3");
        Assert.Contains(Rewards.Achieved(Difficulty.Easy, 1, 10, 1), a => a.Id == "clean10");
        Assert.Contains(Rewards.Achieved(Difficulty.Easy, 1, 0, 7), a => a.Id == "streak7");
        Assert.True(Rewards.Achievements.Length <= 5);
    }
}
