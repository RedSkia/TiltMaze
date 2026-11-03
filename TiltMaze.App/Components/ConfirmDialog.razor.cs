using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace TiltMaze.App.Components;

public partial class ConfirmDialog
{
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string Text { get; set; } = "";
    [Parameter] public string SafeText { get; set; } = "Cancel";
    [Parameter] public string DangerText { get; set; } = "OK";
    [Parameter] public bool Fullscreen { get; set; }   // dækker hele vinduet i stedet for den nærmeste position:relative-boks
    [Parameter] public EventCallback OnSafe { get; set; }
    [Parameter] public EventCallback OnDanger { get; set; }

    private ElementReference safeButton;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await safeButton.FocusAsync();
    }

    private async Task KeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && !e.Repeat)
            await OnSafe.InvokeAsync();
    }
}
