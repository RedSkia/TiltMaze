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
    private bool confirmDelete;   // dialogen "Delete account?" er åben

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
            auth.Logout();
            navigation.NavigateTo("login");
        }
    }
}
