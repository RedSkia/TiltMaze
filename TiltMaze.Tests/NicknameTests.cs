using TiltMaze.Core;

namespace TiltMaze.Tests;

public class NicknameTests
{
    [Theory]
    [InlineData("abc", true)]
    [InlineData("Marcus_01", true)]
    [InlineData("1234567890123456", true)]
    [InlineData("ab", false)]
    [InlineData("12345678901234567", false)]
    [InlineData("with space", false)]
    [InlineData("æøå", false)]
    [InlineData("", false)]
    [InlineData("abc\n", false)]
    public void IsValid(string nickname, bool valid)
    {
        Assert.Equal(valid, Nickname.IsValid(nickname));
    }
}
