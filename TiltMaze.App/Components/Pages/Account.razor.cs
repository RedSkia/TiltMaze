using Microsoft.AspNetCore.Components;
using TiltMaze.App.Services;
using TiltMaze.Core;

namespace TiltMaze.App.Components.Pages;

public partial class Account
{
    [Inject] private Auth auth { get; set; } = null!;
    [Inject] private Api api { get; set; } = null!;
    [Inject] private Biometrics biometrics { get; set; } = null!;
    [Inject] private Sound sound { get; set; } = null!;
    [Inject] private TiltSensor tilt { get; set; } = null!;
    [Inject] private RunQueue queue { get; set; } = null!;
    [Inject] private NavigationManager navigation { get; set; } = null!;

    private string username = "";
    private string error = "";
    private string message = "";
    private bool busy;
    private bool biometricAvailable;   // kontakten vises kun, hvis enheden har fingeraftryk / Windows Hello
    private bool confirmDelete;        // dialogen "Delete account?" er åben

    // Første bogstav af brugernavnet til cirklen på profilen
    private string Initial => auth.Username == "" ? "?" : auth.Username[..1].ToUpperInvariant();

    protected override async Task OnInitializedAsync()
    {
        await auth.Load();
        username = auth.Username;
        biometricAvailable = await biometrics.IsAvailable();
    }

    private async Task SaveUsername()
    {
        error = "";
        message = "";
        username = username.Trim();
        if (!Nickname.IsValid(username))
        {
            error = "The username must be 3-16 letters, digits or underscores.";
            return;
        }

        busy = true;
        error = await api.SetUsername(username);
        busy = false;

        if (error == "")
        {
            await auth.ChangeUsername(username);
            message = "Username saved.";
        }
    }

    // Login er låst: fingeraftrykket låser det op igen
    private async Task Unlock()
    {
        error = "";
        if (!await biometrics.Ask("Unlock your TiltMaze account"))
        {
            error = "The fingerprint was not accepted. Use your password instead.";
            return;
        }
        auth.Unlock();
        username = auth.Username;
        _ = queue.SendPending();
    }

    private void ToggleSound()
    {
        sound.Enabled = !sound.Enabled;
    }

    // Vip eller joystick. Game læser valget, når et løb starter
    private void ToggleTilt()
    {
        tilt.Enabled = !tilt.Enabled;
    }

    private async Task ToggleBiometrics()
    {
        error = "";

        // Slås det til, tjekker vi først, at fingeraftrykket virker
        if (!biometrics.Enabled && !await biometrics.Ask("Confirm to turn on quick unlock"))
        {
            error = "The fingerprint was not accepted.";
            return;
        }
        biometrics.Enabled = !biometrics.Enabled;
    }

    private void Logout()
    {
        auth.Logout();
        navigation.NavigateTo("login");
    }

    private void AskDelete()
    {
        confirmDelete = true;
    }

    private async Task DeleteAccount()
    {
        confirmDelete = false;
        error = "";
        message = "";

        busy = true;
        error = await api.DeleteAccount();
        busy = false;

        if (error == "")
        {
            // Valget om fingeraftryk hører til kontoen, så det ryddes også
            biometrics.Enabled = false;
            biometrics.Asked = false;
            auth.Logout();
            navigation.NavigateTo("login");
        }
    }
}
