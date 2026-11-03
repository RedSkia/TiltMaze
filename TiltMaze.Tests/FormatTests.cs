using TiltMaze.Core;

namespace TiltMaze.Tests;

public class FormatTests
{
    [Theory]
    [InlineData(75.3, "1:15.3")]
    [InlineData(59.96, "1:00.0")]
    [InlineData(4.04, "0:04.0")]
    public void Time(double seconds, string text)
    {
        Assert.Equal(text, Format.Time(seconds));
    }

    [Theory]
    [InlineData(-1.26, "−1.3")]
    [InlineData(0.4, "+0.4")]
    [InlineData(0.02, "0.0")]
    [InlineData(-0.02, "0.0")]
    public void Delta(double seconds, string text)
    {
        Assert.Equal(text, Format.Delta(seconds));
    }
}
