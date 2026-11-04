using TiltMaze.Core;

namespace TiltMaze.Tests;

public class RewardsTests
{
    [Fact]
    public void CompletionRewardGrowsWithDifficulty()
    {
        Assert.Equal(5, Rewards.Completion(Difficulty.Easy));
        Assert.Equal(10, Rewards.Completion(Difficulty.Normal));
        Assert.Equal(20, Rewards.Completion(Difficulty.Hard));
        Assert.Equal(40, Rewards.Completion(Difficulty.Expert));
    }

    [Theory]
    [InlineData(1, 50)]
    [InlineData(3, 50)]
    [InlineData(4, 25)]
    [InlineData(10, 25)]
    [InlineData(11, 0)]
    [InlineData(0, 0)]
    public void RankBonusByRank(int rank, int expected)
    {
        Assert.Equal(expected, Rewards.RankBonus(rank));
    }

    [Theory]
    [InlineData(8, 0, 25)]    // første gang i top 10
    [InlineData(2, 0, 50)]    // første gang i top 3
    [InlineData(2, 25, 25)]   // fra top 10 til top 3: kun forskellen
    [InlineData(2, 50, 0)]    // allerede betalt
    [InlineData(8, 50, 0)]    // lavere end før giver ingenting
    [InlineData(20, 0, 0)]    // uden for top 10
    public void RankBonusIsPaidOnlyOnce(int rank, int alreadyPaid, int expected)
    {
        Assert.Equal(expected, Rewards.RankBonusToPay(rank, alreadyPaid));
    }

    [Fact]
    public void FirstColorIsFreeAndIdsAreUnique()
    {
        Assert.Equal(0, Rewards.BallColors[0].Price);
        Assert.Equal(Rewards.BallColors.Length, Rewards.BallColors.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void UnknownColorIdGivesTheFreeColor()
    {
        Assert.Equal(Rewards.BallColors[0], Rewards.ColorById("nope"));
        Assert.Equal(Rewards.BallColors[0], Rewards.ColorById(null));
        Assert.Equal("ruby", Rewards.ColorById("ruby").Id);
    }
}
