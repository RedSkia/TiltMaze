using Microsoft.AspNetCore.Components;
using TiltMaze.Core;

namespace TiltMaze.App.Components.Play;

public partial class BallPreview
{
    [Parameter] public BallColor Color { get; set; } = Rewards.BallColors[0];
    [Parameter] public string Pattern { get; set; } = "plain";
    [Parameter] public string Id { get; set; } = "";   // gør farveforløbets id unikt på siden
}
