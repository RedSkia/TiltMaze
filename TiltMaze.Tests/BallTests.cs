using TiltMaze.Core;

namespace TiltMaze.Tests;

public class BallTests
{
    // Kører kuglen rundt med tilfældig vip og tjekker hele tiden, at den aldrig kommer gennem en væg:
    // når kuglens midte skifter celle, skal det være til en nabocelle uden væg imellem.
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void BallNeverPassesThroughAWall(int levelNumber)
    {
        var level = new Level(levelNumber);
        var random = new Random(levelNumber);
        var ball = new Ball(level.Segments, level.Start.X + 0.5, level.Start.Y + 0.5);
        double ax = 0, ay = 0;
        bool reachedMaxSpeed = false;

        // 60 sekunder i 60 billeder pr. sekund. Vippet skifter ca. hvert halve sekund.
        for (int frame = 0; frame < 3600; frame++)
        {
            if (frame % 30 == 0)
            {
                ax = random.Next(-1, 2);
                ay = random.Next(-1, 2);
            }

            int oldX = (int)Math.Floor(ball.X);
            int oldY = (int)Math.Floor(ball.Y);
            ball.Update(1.0 / 60, ax, ay);
            int newX = (int)Math.Floor(ball.X);
            int newY = (int)Math.Floor(ball.Y);

            Assert.InRange(newX, 0, level.Width - 1);
            Assert.InRange(newY, 0, level.Height - 1);
            if (ball.VX * ball.VX + ball.VY * ball.VY > (Ball.MaxSpeed - 0.1) * (Ball.MaxSpeed - 0.1))
                reachedMaxSpeed = true;

            if (newX == oldX && newY == oldY) continue;

            // Kun ét skridt ad gangen og ikke på tværs af et hjørne
            Assert.Equal(1, Math.Abs(newX - oldX) + Math.Abs(newY - oldY));
            Wall side = newX > oldX ? Wall.East : newX < oldX ? Wall.West : newY > oldY ? Wall.South : Wall.North;
            Assert.False(level.Walls[oldX, oldY].HasFlag(side), $"Ball went through the {side} wall of {oldX},{oldY}");
        }

        Assert.True(reachedMaxSpeed, "The test never reached max speed");
    }

    [Fact]
    public void BallStopsAtTheOuterWall()
    {
        var level = new Level(1);
        var ball = new Ball(level.Segments, 0.5, 0.5);

        // Hold fuld vip mod øst og syd i 10 sekunder
        for (int i = 0; i < 600; i++)
            ball.Update(1.0 / 60, 1, 1);

        Assert.InRange(ball.X, 0, level.Width);
        Assert.InRange(ball.Y, 0, level.Height);
    }
}
