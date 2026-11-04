using Microsoft.AspNetCore.Components;
using TiltMaze.App.Services;

namespace TiltMaze.App.Components.Layout;

public partial class MainLayout
{
    [Inject] private NavigationManager navigation { get; set; } = null!;
    [Inject] private Auth auth { get; set; } = null!;
    [Inject] private Biometrics biometrics { get; set; } = null!;
    [Inject] private RunQueue queue { get; set; } = null!;
    [Inject] private Sound sound { get; set; } = null!;

    private bool ready;               // det gemte login er læst (og låst op, hvis det kan)
    private string routeClass = "";   // "on-play" skjuler menuen, så banen får hele skærmen

    protected override async Task OnInitializedAsync()
    {
        await auth.Load();
        if (auth.IsLocked)
            await UnlockWithBiometrics();
        ready = true;
    }

    // Fingeraftrykket låser det gemte login op. Annulleres det, eller er der ingen sensor, forbliver login låst:
    // man spiller som gæst og kan låse op eller skrive kodeordet på Account-siden
    private async Task UnlockWithBiometrics()
    {
        if (!await biometrics.IsAvailable())
            return;
        if (!await biometrics.Ask("Unlock your TiltMaze account"))
            return;

        auth.Unlock();
        _ = queue.SendPending();   // RunQueue nåede ikke at sende, mens login var låst
    }

    // JavaScript skal vide, om lyden er slået til (til klik på knapper)
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            sound.Sync();
    }

    // Kører ved hver sideskift
    protected override void OnParametersSet()
    {
        var route = navigation.ToBaseRelativePath(navigation.Uri);
        routeClass = route.StartsWith("play") ? "on-play" : "";
    }
}
