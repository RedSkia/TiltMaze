using Microsoft.AspNetCore.Components;

namespace TiltMaze.App.Components.Layout;

public partial class MainLayout
{
    [Inject] private NavigationManager navigation { get; set; } = null!;

    private string routeClass = "";   // "on-play" skjuler menuen, så banen får hele skærmen

    // Kører ved hver sideskift
    protected override void OnParametersSet()
    {
        var route = navigation.ToBaseRelativePath(navigation.Uri);
        routeClass = route.StartsWith("play") ? "on-play" : "";
    }
}
