using TiltMaze.Core;

namespace TiltMaze.Tests;

public class CoinTests
{
    const int Seeds = 100;

    public static IEnumerable<object[]> Difficulties => Enum.GetValues<Difficulty>().Select(d => new object[] { d });

    [Theory]
    [MemberData(nameof(Difficulties))]
    public void SameSeedGivesSameCoins(Difficulty difficulty)
    {
        for (int seed = 1; seed <= Seeds; seed++)
            Assert.Equal(new Course(difficulty, seed).Coins, new Course(difficulty, seed).Coins);
    }

    [Theory]
    [MemberData(nameof(Difficulties))]
    public void CoinsNeverOnStartGoalOrHolesAndNeverTwiceInACell(Difficulty difficulty)
    {
        for (int seed = 1; seed <= Seeds; seed++)
        {
            var course = new Course(difficulty, seed);
            Assert.DoesNotContain(course.Start, course.Coins);
            Assert.DoesNotContain(course.Goal, course.Coins);
            foreach (Hole hole in course.Holes)
                Assert.DoesNotContain(hole.Cell, course.Coins);
            Assert.Equal(course.Coins.Count, course.Coins.Distinct().Count());
        }
    }

    [Theory]
    [InlineData(Difficulty.Easy, 3)]
    [InlineData(Difficulty.Normal, 5)]
    [InlineData(Difficulty.Hard, 7)]
    [InlineData(Difficulty.Expert, 9)]
    public void EachDifficultyHasItsNumberOfCoins(Difficulty difficulty, int expected)
    {
        for (int seed = 1; seed <= Seeds; seed++)
            Assert.Equal(expected, new Course(difficulty, seed).Coins.Count);
    }

    [Fact]
    public void BallOnACoinTouchesIt()
    {
        var course = new Course(Difficulty.Easy, 7);
        var coin = course.Coins[0];
        var ball = new Ball(course.Segments, coin.X + 0.5, coin.Y + 0.5);

        Assert.True(course.TouchesCoin(ball, 0));
    }

    [Fact]
    public void BallJustOutOfReachDoesNotTouchTheCoin()
    {
        var course = new Course(Difficulty.Easy, 7);
        var coin = course.Coins[0];
        double reach = Ball.Radius + Course.CoinRadius;

        var near = new Ball(course.Segments, coin.X + 0.5 + reach - 0.01, coin.Y + 0.5);
        var far = new Ball(course.Segments, coin.X + 0.5 + reach + 0.01, coin.Y + 0.5);

        Assert.True(course.TouchesCoin(near, 0));
        Assert.False(course.TouchesCoin(far, 0));
    }

    [Fact]
    public void BallOnTheStartDoesNotTouchAnyCoin()
    {
        var course = new Course(Difficulty.Expert, 3);
        var ball = new Ball(course.Segments, course.Start.X + 0.5, course.Start.Y + 0.5);

        for (int i = 0; i < course.Coins.Count; i++)
            Assert.False(course.TouchesCoin(ball, i));
    }
}
