using Microsoft.AspNetCore.Components;
using TiltMaze.Core;

namespace TiltMaze.App.Components.Play;

public partial class TrailView
{
    [Parameter] public Trail Points { get; set; } = null!;
    [Parameter] public string Kind { get; set; } = "none";
    [Parameter] public double Size { get; set; } = Ball.Radius;   // kuglens radius

    private string[] Colors => ShopItems.TrailColors(Kind);
}
