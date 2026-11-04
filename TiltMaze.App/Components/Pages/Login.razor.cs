using Microsoft.AspNetCore.Components;
using TiltMaze.App.Services;
using TiltMaze.Core;

namespace TiltMaze.App.Components.Pages;

public partial class Login : IDisposable
{
    [Inject] private Auth auth { get; set; } = null!;
    [Inject] private Biometrics biometrics { get; set; } = null!;
    [Inject] private RunQueue queue { get; set; } = null!;
    [Inject] private NavigationManager navigation { get; set; } = null!;

    private string username = "";
    private string password = "";
    private string error = "";
    private bool creating;   // false = log ind, true = opret konto
    private bool busy;
    private bool showPassword;   // true = kodeordet vises som tekst

    // Er navnet ledigt? "" (intet at vise), "checking", "available", "taken", "invalid" eller "unknown" (uden net)
    private string availability = "";
    private CancellationTokenSource? typing;

    private string AvailabilityText => availability switch
    {
        "checking" => "Checking...",
        "available" => "Username is free",
        "taken" => "That username is taken",
        "invalid" => "3-16 letters, digits or underscores",
        _ => "Could not check the name right now"
    };

    protected override async Task OnInitializedAsync()
    {
        // Allerede logget ind: videre til top-listen
        await auth.Load();
        if (auth.IsLoggedIn)
            navigation.NavigateTo("leaderboard");
    }

    private void SetMode(bool create)
    {
        creating = create;
        error = "";
        _ = UsernameChanged();
    }

    // Mens man skriver på "Create account": spørg serveren, om navnet er ledigt, når man har holdt pause i 400 ms
    private async Task UsernameChanged()
    {
        typing?.Cancel();
        typing = null;
        string name = username.Trim();
        if (!creating || name == "")
        {
            availability = "";
            return;
        }
        if (!Nickname.IsValid(name))
        {
            availability = "invalid";
            return;
        }

        var token = (typing = new CancellationTokenSource()).Token;
        availability = "checking";
        try
        {
            await Task.Delay(400, token);
        }
        catch (TaskCanceledException)
        {
            return;   // der blev skrevet videre
        }

        bool? free = await auth.IsAvailable(name);
        if (token.IsCancellationRequested)
            return;
        availability = free == null ? "unknown" : free.Value ? "available" : "taken";
        StateHasChanged();
    }

    private async Task Submit()
    {
        error = "";
        username = username.Trim();

        // Enkelt tjek i appen. Serveren tjekker det hele igen
        if (!Nickname.IsValid(username))
        {
            error = "The username must be 3-16 letters, digits or underscores.";
            return;
        }
        if (creating && password.Length < 8)
        {
            error = "The password must be at least 8 characters.";
            return;
        }
        if (password == "")
        {
            error = "Enter your password.";
            return;
        }

        busy = true;
        var result = creating ? await auth.Register(username, password) : await auth.Login(username, password);
        busy = false;

        if (!result.Ok)
        {
            error = result.Error;
            return;
        }

        // Send de rekorder, der ventede på en konto
        password = "";
        _ = queue.SendPending();
        await OfferBiometrics();
        navigation.NavigateTo("leaderboard");
    }

    // Spørger én gang efter første login, om appen skal låses op med fingeraftryk / Windows Hello.
    // Spørges der ikke igen, kan det slås til under Account
    private async Task OfferBiometrics()
    {
        if (biometrics.Asked || !await biometrics.IsAvailable())
            return;

        biometrics.Asked = true;
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
            return;
        biometrics.Enabled = await page.DisplayAlertAsync("Quick unlock",
            "Unlock TiltMaze with your fingerprint or Windows Hello next time?", "Yes", "No");
    }

    public void Dispose()
    {
        typing?.Cancel();
    }
}
