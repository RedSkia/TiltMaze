using Microsoft.AspNetCore.Components;
using TiltMaze.App.Services;
using TiltMaze.Core;

namespace TiltMaze.App.Components.Pages;

public partial class Start
{
    [Inject] private Scores scores { get; set; } = null!;
    [Inject] private NavigationManager navigation { get; set; } = null!;

    private readonly Difficulty[] difficulties = Enum.GetValues<Difficulty>();
    private readonly int dailySeed = Course.DailySeed();

    // Ny tilfældig labyrint
    private void PlayRandom(Difficulty difficulty)
    {
        Open(difficulty, Course.NewSeed());
    }

    // Dagens labyrint: ens for alle, fordi seed er datoen
    private void PlayDaily(Difficulty difficulty)
    {
        Open(difficulty, dailySeed);
    }

    private void Open(Difficulty difficulty, int seed)
    {
        navigation.NavigateTo("play/" + difficulty.ToString().ToLower() + "/" + seed);
    }
}
