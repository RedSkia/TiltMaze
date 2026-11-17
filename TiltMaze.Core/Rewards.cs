namespace TiltMaze.Core;

// En farve til kuglen i butikken. Light/Mid/Dark er de tre toner i kuglens farveforløb.
public record BallColor(string Id, string Name, int Price, string Light, string Mid, string Dark);

// En præstation der giver mønter én gang
public record Achievement(string Id, string Name, int Coins);

// Hvor mange mønter man får og hvad ting koster. Alle tal ligger her samlet.
public static class Rewards
{
    public const int CoinValue = 1;   // en mønt i labyrinten

    // Første gang man klarer en bestemt labyrint
    public static int Completion(Difficulty difficulty)
    {
        return difficulty switch
        {
            Difficulty.Easy => 5,
            Difficulty.Normal => 10,
            Difficulty.Hard => 20,
            _ => 40,
        };
    }

    // Stjerner: tid pr. celle på den korteste vej. Hurtigere end ThreeStarPerCell = 3 stjerner osv.
    public const double ThreeStarPerCell = 0.5;
    public const double TwoStarPerCell = 0.9;
    public const int StreakBonus = 5;   // mønter for første labyrint på en ny dag i stribe

    public static int Stars(Course course, double seconds)
    {
        int cells = MazeGenerator.Distances(course.Walls, course.Start)[course.Goal.X, course.Goal.Y];
        if (seconds <= cells * ThreeStarPerCell) return 3;
        if (seconds <= cells * TwoStarPerCell) return 2;
        return 1;
    }

    // Streak efter at have klaret en labyrint i dag: samme dag ændrer intet, dagen efter lægger en til, ellers starter den forfra
    public static int NextStreak(DateOnly? last, DateOnly today, int streak)
    {
        if (last == today) return streak;
        if (last == today.AddDays(-1)) return streak + 1;
        return 1;
    }

    // Streak der vises: 0 hvis der er gået mere end én dag siden sidste labyrint
    public static int ShownStreak(DateOnly? last, DateOnly today, int streak)
    {
        return last >= today.AddDays(-1) ? streak : 0;
    }

    public static readonly Achievement[] Achievements =
    [
        new("first", "Clear a maze", 10),
        new("normal", "Clear Normal", 20),
        new("hard", "Clear Hard", 50),
        new("expert", "Clear Expert", 100),
        new("stars3", "Get 3 stars", 30),
        new("hard3", "Get 3 stars on Hard", 75),
        new("expert3", "Get 3 stars on Expert", 150),
        new("clean10", "10 runs without falling", 50),
        new("clean25", "25 runs without falling", 100),
        new("streak3", "3 day streak", 15),
        new("streak7", "7 day streak", 40),
        new("streak30", "30 day streak", 150),
    ];

    // De præstationer der er opfyldt lige nu (kalderen giver kun dem, man ikke har)
    public static List<Achievement> Achieved(Difficulty difficulty, int stars, int cleanRuns, int streak)
    {
        var done = new List<string> { "first" };
        if (difficulty == Difficulty.Normal) done.Add("normal");
        if (difficulty == Difficulty.Hard) done.Add("hard");
        if (difficulty == Difficulty.Expert) done.Add("expert");
        if (stars == 3) done.Add("stars3");
        if (difficulty == Difficulty.Hard && stars == 3) done.Add("hard3");
        if (difficulty == Difficulty.Expert && stars == 3) done.Add("expert3");
        if (cleanRuns >= 10) done.Add("clean10");
        if (cleanRuns >= 25) done.Add("clean25");
        if (streak >= 3) done.Add("streak3");
        if (streak >= 7) done.Add("streak7");
        if (streak >= 30) done.Add("streak30");
        return Achievements.Where(a => done.Contains(a.Id)).ToList();
    }

    // Bonus for en plads på top-listen: top 3 giver 50, top 10 giver 25
    public static int RankBonus(int rank)
    {
        if (rank >= 1 && rank <= 3) return 50;
        if (rank >= 1 && rank <= 10) return 25;
        return 0;
    }

    // Hvad der mangler at blive betalt, når bonussen allerede er givet én gang på labyrinten.
    // En højere bonus erstatter en lavere (man får kun forskellen), og en lavere giver ingenting.
    public static int RankBonusToPay(int rank, int alreadyPaid)
    {
        return Math.Max(0, RankBonus(rank) - alreadyPaid);
    }

    // Kuglens farver. Den første er gratis og er standard.
    public static readonly BallColor[] BallColors =
    [
        new("orange", "Sunset", 0, "#ffa45c", "#ff7a29", "#b8400c"),
        new("sky", "Sky", 30, "#9fdcff", "#38a8f0", "#0d5a94"),
        new("lime", "Lime", 40, "#d6ff8a", "#8ede2a", "#3f7a0a"),
        new("pink", "Bubblegum", 60, "#ffc2e0", "#ff5fae", "#a3175f"),
        new("violet", "Grape", 80, "#d9c2ff", "#9a6bff", "#4a1fa8"),
        new("ruby", "Ruby", 100, "#ffb3ba", "#ee3b4d", "#7d0c1d"),
        new("mint", "Mint", 120, "#c4fff0", "#2de2c8", "#0b6e60"),
        new("gold", "Gold", 200, "#fff0b0", "#ffc857", "#a56d00"),
    ];

    // Farven med dette id. Kender vi ikke id'et, bruges den gratis.
    public static BallColor ColorById(string? id)
    {
        return BallColors.FirstOrDefault(c => c.Id == id) ?? BallColors[0];
    }
}
