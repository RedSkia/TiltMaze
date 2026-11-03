using Microsoft.AspNetCore.Components;
using TiltMaze.App.Services;

namespace TiltMaze.App.Components.Pages;

public partial class Levels
{
    [Inject] private Scores scores { get; set; } = null!;
}
