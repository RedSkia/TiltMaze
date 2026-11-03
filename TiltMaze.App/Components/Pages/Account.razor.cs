using Microsoft.AspNetCore.Components;
using TiltMaze.App.Services;
using TiltMaze.Core;

namespace TiltMaze.App.Components.Pages;

public partial class Account
{
    [Inject] private Auth auth { get; set; } = null!;
    [Inject] private Api api { get; set; } = null!;
    [Inject] private NavigationManager navigation { get; set; } = null!;

    private string nickname = "";
    private string error = "";
    private string message = "";
    private bool busy;

    protected override async Task OnInitializedAsync()
    {
        await auth.Load();
        nickname = auth.Nickname;
    }

    private async Task SaveNickname()
    {
        error = "";
        message = "";
        nickname = nickname.Trim();
        if (!Nickname.IsValid(nickname))
        {
            error = "The nickname must be 3-16 letters, digits or underscores.";
            return;
        }

        busy = true;
        error = await api.SetNickname(nickname);
        busy = false;

        if (error == "")
        {
            await auth.SetNickname(nickname);
            message = "Nickname saved.";
        }
    }

    private void Logout()
    {
        auth.Logout();
        navigation.NavigateTo("login");
    }

    private async Task DeleteAccount()
    {
        error = "";
        message = "";

        var page = Application.Current!.Windows[0].Page!;
        var yes = await page.DisplayAlertAsync("Delete account",
            "This deletes your account, your times and your ghosts on the server. This cannot be undone.",
            "Delete my account", "Cancel");
        if (!yes)
            return;

        busy = true;
        error = await api.DeleteAccount();
        busy = false;

        if (error == "")
        {
            auth.Logout();
            navigation.NavigateTo("login");
        }
    }
}
