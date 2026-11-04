using Microsoft.AspNetCore.Components;
using TiltMaze.App.Services;
using TiltMaze.Core;

namespace TiltMaze.App.Components.Pages;

public partial class Shop
{
    [Inject] private Wallet wallet { get; set; } = null!;

    private int coins;
    private string equippedId = "";

    protected override void OnInitialized()
    {
        Refresh();
    }

    private void Refresh()
    {
        coins = wallet.Coins;
        equippedId = wallet.Equipped.Id;
    }

    private void Buy(BallColor color)
    {
        wallet.Buy(color);
        Refresh();
    }

    private void Equip(BallColor color)
    {
        wallet.Equip(color);
        Refresh();
    }
}
