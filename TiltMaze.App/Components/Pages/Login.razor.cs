using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using TiltMaze.App.Services;

namespace TiltMaze.App.Components.Pages;

public partial class Login
{
    [Inject] private Auth auth { get; set; } = null!;
    [Inject] private RunQueue queue { get; set; } = null!;
    [Inject] private NavigationManager navigation { get; set; } = null!;

    private string email = "";
    private string nickname = "";
    private string password = "";
    private string error = "";
    private bool creating;   // false = log ind, true = opret konto
    private bool busy;

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
    }

    private async Task Submit()
    {
        error = "";
        email = email.Trim();
        nickname = nickname.Trim();

        // Enkelt tjek i appen. Serveren tjekker det hele igen
        if (!email.Contains('@') || email.Length < 3)
        {
            error = "Enter a valid email.";
            return;
        }
        if (creating && !Regex.IsMatch(nickname, "^[A-Za-z0-9_]{3,16}$"))
        {
            error = "The nickname must be 3-16 letters, digits or underscores.";
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
        var result = creating ? await auth.Register(email, password, nickname) : await auth.Login(email, password);
        busy = false;

        if (!result.Ok)
        {
            error = result.Error;
            return;
        }

        // Send de rekorder, der ventede på en konto
        password = "";
        _ = queue.SendPending();
        navigation.NavigateTo("leaderboard");
    }
}
