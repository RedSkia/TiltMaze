using Microsoft.AspNetCore.Components;
using TiltMaze.App.Services;
using TiltMaze.Core;

namespace TiltMaze.App.Components.Pages;

public partial class Leaderboard
{
    [Inject] private Api api { get; set; } = null!;
    [Inject] private Auth auth { get; set; } = null!;

    private readonly Difficulty[] difficulties = Enum.GetValues<Difficulty>();
    private Difficulty difficulty = Difficulty.Normal;
    private bool daily = true;
    private string seedText = "";
    private List<LeaderboardRow>? rows;
    private string error = "";
    private bool offline;
    private bool busy;

    protected override async Task OnInitializedAsync()
    {
        await auth.Load();
        await Load();
    }

    private async Task SetDifficulty(Difficulty value)
    {
        difficulty = value;
        await Load();
    }

    private async Task SetDaily(bool value)
    {
        daily = value;
        error = "";
        if (daily)
            await Load();
        else
            rows = null;
    }

    // Link til spillet med den valgte sværhedsgrad og seed (dagens seed, hvis "Daily maze" er valgt)
    private string PlayLink => daily
        ? $"play/{difficulty}/{Course.DailySeed()}"
        : $"play/{difficulty}/{seedText.Trim()}";

    private async Task Load()
    {
        error = "";
        offline = false;

        int seed = Course.DailySeed();
        if (!daily && (!int.TryParse(seedText.Trim(), out seed) || seed < 0))
        {
            rows = null;
            error = "Enter a seed (a whole number, 0 or higher).";
            return;
        }

        busy = true;
        rows = await api.GetLeaderboard(difficulty, seed);
        busy = false;
        offline = rows == null;
    }
}
