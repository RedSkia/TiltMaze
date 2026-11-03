using Microsoft.AspNetCore.Components;
using TiltMaze.App.Services;

namespace TiltMaze.App.Components.Layout;

public partial class NavMenu
{
    [Inject] private Scores scores { get; set; } = null!;
}
