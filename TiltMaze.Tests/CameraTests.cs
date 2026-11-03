using TiltMaze.Core;

namespace TiltMaze.Tests;

public class CameraTests
{
    [Fact]
    public void ViewBiggerThanMazeStaysInTheMiddle()
    {
        var camera = new Camera(5, 7);
        camera.SetView(20, 20);
        camera.Snap(0.5, 0.5);
        Assert.Equal(2.5, camera.X, 6);
        Assert.Equal(3.5, camera.Y, 6);
    }

    [Fact]
    public void EdgesStayInsideTheMaze()
    {
        var camera = new Camera(11, 16);
        camera.SetView(4, 6);

        camera.Snap(0.5, 0.5);
        Assert.Equal(-Camera.Margin, camera.Left, 6);
        Assert.Equal(-Camera.Margin, camera.Top, 6);

        camera.Snap(10.5, 15.5);
        Assert.Equal(11 + Camera.Margin, camera.Left + camera.ViewWidth, 6);
        Assert.Equal(16 + Camera.Margin, camera.Top + camera.ViewHeight, 6);
    }

    [Fact]
    public void FollowMovesTowardsTheBallWithoutOvershooting()
    {
        var camera = new Camera(11, 16);
        camera.SetView(4, 6);
        camera.Snap(3, 4);

        camera.Follow(6, 8, 1.0 / 60);
        Assert.True(camera.X > 3 && camera.X < 6);
        Assert.True(camera.Y > 4 && camera.Y < 8);

        for (int i = 0; i < 600; i++)
            camera.Follow(6, 8, 1.0 / 60);
        Assert.Equal(6, camera.X, 3);
        Assert.Equal(8, camera.Y, 3);
    }
}
