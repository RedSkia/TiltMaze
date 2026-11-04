using Microsoft.AspNetCore.Components;
using TiltMaze.App.Services;
using TiltMaze.Core;

namespace TiltMaze.App.Components.Pages;

public partial class Shop
{
    [Inject] private Wallet wallet { get; set; } = null!;
    [Inject] private Sound sound { get; set; } = null!;

    // Fanerne øverst
    private record Tab(ShopCategory Category, string Label);

    private static readonly Tab[] tabs =
    [
        new(ShopCategory.Color, "Colours"),
        new(ShopCategory.Trail, "Trails"),
        new(ShopCategory.Pattern, "Patterns"),
        new(ShopCategory.GoalEffect, "Finish"),
        new(ShopCategory.Theme, "Themes"),
    ];

    private ShopCategory category = ShopCategory.Color;
    private int coins;
    private string equippedId = "";
    private BallColor ballColor = Rewards.BallColors[0];   // kuglen i forhåndsvisningerne
    private readonly Trail previewTrail = new();

    protected override void OnInitialized()
    {
        // Et lige spor til forhåndsvisningen, ældste punkt først
        for (int i = 0; i < Trail.Length; i++)
            previewTrail.Add(-3 + i * 0.2, 0);

        Refresh();
    }

    private void Select(ShopCategory selected)
    {
        category = selected;
        Refresh();
    }

    private void Refresh()
    {
        coins = wallet.Coins;
        ballColor = wallet.EquippedColor;
        equippedId = wallet.EquippedId(category);
    }

    private void Buy(ShopItem item)
    {
        if (wallet.Buy(item))
            sound.Buy();
        Refresh();
    }

    private void Equip(ShopItem item)
    {
        wallet.Equip(item);
        Refresh();
    }
}
