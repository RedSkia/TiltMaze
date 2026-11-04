using TiltMaze.Core;

namespace TiltMaze.Tests;

public class ShopItemsTests
{
    public static IEnumerable<object[]> Categories => Enum.GetValues<ShopCategory>().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Categories))]
    public void EachCategoryHasOneFreeDefaultFirstAndPaidRest(ShopCategory category)
    {
        var items = ShopItems.In(category).ToList();
        Assert.True(items.Count >= 2);
        Assert.Equal(0, items[0].Price);
        Assert.All(items.Skip(1), i => Assert.InRange(i.Price, 30, 250));
        Assert.Equal(items[0], ShopItems.Default(category));
    }

    [Fact]
    public void IdsAreUniqueInEachCategoryAndOwnedKeysAreUnique()
    {
        foreach (var category in Enum.GetValues<ShopCategory>())
        {
            var ids = ShopItems.In(category).Select(i => i.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }
        var keys = ShopItems.All.Select(ShopItems.OwnedKey).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void ColorsKeepTheirOldOwnedKeyAndOthersGetTheCategory()
    {
        // Gamle ejede farver ligger som bare id'et i SQLite og skal stadig virke
        foreach (var color in Rewards.BallColors)
            Assert.Equal(color.Id, ShopItems.OwnedKey(ShopItems.ById(ShopCategory.Color, color.Id)));
        Assert.Equal("trail:fire", ShopItems.OwnedKey(ShopItems.ById(ShopCategory.Trail, "fire")));
        Assert.Equal("theme:neon", ShopItems.OwnedKey(ShopItems.ById(ShopCategory.Theme, "neon")));
    }

    [Fact]
    public void ColorsInTheCatalogueMatchBallColors()
    {
        Assert.Equal(Rewards.BallColors.Select(c => (c.Id, c.Price)),
            ShopItems.In(ShopCategory.Color).Select(i => (i.Id, i.Price)));
    }

    [Fact]
    public void UnknownIdGivesTheFreeItem()
    {
        Assert.Equal("none", ShopItems.ById(ShopCategory.Trail, "nope").Id);
        Assert.Equal("confetti", ShopItems.ById(ShopCategory.GoalEffect, null).Id);
    }

    [Fact]
    public void EveryPaidTrailHasColorsAndNoTrailHasNone()
    {
        Assert.Empty(ShopItems.TrailColors("none"));
        foreach (var trail in ShopItems.In(ShopCategory.Trail).Skip(1))
            Assert.NotEmpty(ShopItems.TrailColors(trail.Id));
    }

    [Fact]
    public void TrailKeepsTheLatestPositionsOldestFirst()
    {
        var trail = new Trail();
        Assert.Equal(0, trail.Count);

        for (int i = 1; i <= Trail.Length + 3; i++)
            trail.Add(i, -i);

        Assert.Equal(Trail.Length, trail.Count);
        Assert.Equal((4.0, -4.0), trail.At(0));
        Assert.Equal((Trail.Length + 3.0, -(Trail.Length + 3.0)), trail.At(Trail.Length - 1));
    }

    [Fact]
    public void TrailCountsUpAndClears()
    {
        var trail = new Trail();
        trail.Add(1, 1);
        trail.Add(2, 2);
        Assert.Equal(2, trail.Count);
        Assert.Equal((1.0, 1.0), trail.At(0));

        trail.Clear();
        Assert.Equal(0, trail.Count);
        trail.Add(9, 9);
        Assert.Equal((9.0, 9.0), trail.At(0));
    }
}
