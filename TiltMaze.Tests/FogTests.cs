using TiltMaze.Core;
using Xunit;

namespace TiltMaze.Tests;

public class FogTests
{
    [Fact]
    public void EasyAndNormalHaveNoFog()
    {
        Assert.Equal(0, DifficultySettings.For(Difficulty.Easy).FogRadius);
        Assert.Equal(0, DifficultySettings.For(Difficulty.Normal).FogRadius);
    }

    [Fact]
    public void ExpertSeesLessThanHard()
    {
        double hard = DifficultySettings.For(Difficulty.Hard).FogRadius;
        double expert = DifficultySettings.For(Difficulty.Expert).FogRadius;
        Assert.True(hard > 0);
        Assert.True(expert > 0 && expert < hard);
    }
}
