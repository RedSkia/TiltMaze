using TiltMaze.Core;

namespace TiltMaze.Tests;

public class BallTests
{
    // Kører kuglen rundt med tilfældig vip og tjekker hele tiden, at den aldrig kommer gennem en væg:
    // når kuglens midte skifter celle, skal det være til en nabocelle uden væg imellem.
    [Theory]
    [InlineData(Difficulty.Easy, 1)]
    [InlineData(Difficulty.Normal, 6)]
    [InlineData(Difficulty.Hard, 12)]
    [InlineData(Difficulty.Expert, 7)]
    public void BallNeverPassesThroughAWall(Difficulty difficulty, int seed)
    {
        var course = new Course(difficulty, seed);
        var random = new Random(seed);
        var ball = new Ball(course.Segments, course.Start.X + 0.5, course.Start.Y + 0.5);
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

            Assert.InRange(newX, 0, course.Width - 1);
            Assert.InRange(newY, 0, course.Height - 1);
            if (ball.Speed > Ball.MaxSpeed - 0.1)
                reachedMaxSpeed = true;
            if (newX == oldX && newY == oldY) continue;

            // Højst ét felt ad gangen (også på skrå), og der skal være en åben vej dertil
            Assert.InRange(newX - oldX, -1, 1);
            Assert.InRange(newY - oldY, -1, 1);
            Assert.True(CanMove(course.Walls, oldX, oldY, newX - oldX, newY - oldY), $"Ball went through a wall at {oldX},{oldY}");
        }
        Assert.True(reachedMaxSpeed, "The test never reached max speed");
    }

    // Kan man gå fra (x, y) ét felt i retning (dx, dy) uden at gå gennem en væg? På skrå (fx i et rum) må begge veje om hjørnet være åbne.
    static bool CanMove(Wall[,] walls, int x, int y, int dx, int dy)
    {
        if (dx != 0 && dy != 0)
            return CanMove(walls, x, y, dx, 0) && CanMove(walls, x + dx, y, 0, dy)
                || CanMove(walls, x, y, 0, dy) && CanMove(walls, x, y + dy, dx, 0);

        Wall side = dx > 0 ? Wall.East : dx < 0 ? Wall.West : dy > 0 ? Wall.South : Wall.North;
        return !walls[x, y].HasFlag(side);
    }

    [Fact]
    public void BallStopsAtTheOuterWall()
    {
        var course = new Course(Difficulty.Easy, 1);
        var ball = new Ball(course.Segments, 0.5, 0.5);

        // Hold fuld vip mod øst og syd i 10 sekunder
        for (int i = 0; i < 600; i++)
            ball.Update(1.0 / 60, 1, 1);

        Assert.InRange(ball.X, Ball.Radius, course.Width - Ball.Radius);
        Assert.InRange(ball.Y, Ball.Radius, course.Height - Ball.Radius);
    }
}
