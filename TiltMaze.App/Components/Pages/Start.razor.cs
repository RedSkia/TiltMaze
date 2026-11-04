using System.Globalization;
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
    private readonly string today = DateTime.UtcNow.ToString("ddd d MMM", CultureInfo.InvariantCulture);

    // Tid til næste daglige labyrint (UTC-midnat), regnet ud når siden åbnes
    private readonly string timeLeft = TimeUntilNextDaily();

    // Den valgte sværhedsgrad huskes til næste gang
    private Difficulty selected = Difficulty.Normal;

    protected override void OnInitialized()
    {
        string saved = Preferences.Default.Get("difficulty", "Normal");
        if (Enum.TryParse(saved, out Difficulty difficulty))
            selected = difficulty;
    }

    private void Select(Difficulty difficulty)
    {
        selected = difficulty;
        Preferences.Default.Set("difficulty", difficulty.ToString());
    }

    // Dagens labyrint: ens for alle, fordi seed er datoen
    private void PlayDaily()
    {
        Open(selected, dailySeed);
    }

    // Ny tilfældig labyrint
    private void PlayRandom()
    {
        Open(selected, Course.NewSeed());
    }

    private void Open(Difficulty difficulty, int seed)
    {
        navigation.NavigateTo("play/" + difficulty.ToString().ToLower() + "/" + seed);
    }

    private static string TimeUntilNextDaily()
    {
        DateTime now = DateTime.UtcNow;
        TimeSpan left = now.Date.AddDays(1) - now;
        return (int)left.TotalHours + "h " + left.Minutes + "m";
    }

    private static string TimeText(double? seconds)
    {
        return seconds == null ? "-" : Format.Time(seconds.Value);
    }
}
