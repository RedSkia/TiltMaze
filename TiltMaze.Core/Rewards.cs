namespace TiltMaze.Core;

// En farve til kuglen i butikken. Light/Mid/Dark er de tre toner i kuglens farveforløb.
public record BallColor(string Id, string Name, int Price, string Light, string Mid, string Dark);

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
