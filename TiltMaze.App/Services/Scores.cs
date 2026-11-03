using System.Globalization;

namespace TiltMaze.App.Services;

// Bedste tid pr. bane. Gemmes lokalt på enheden med Preferences (SQLite kommer senere).
public class Scores
{
    public const int LevelCount = 12;

    // Bedste tid i sekunder, eller null hvis banen ikke er klaret endnu
    public double? Best(int level)
    {
        double seconds = Preferences.Default.Get("best-" + level, 0.0);
        if (seconds > 0)
            return seconds;
        return null;
    }

    // Gemmer tiden, hvis den er bedre end den gamle. Giver true, hvis det er en ny rekord.
    public bool Save(int level, double seconds)
    {
        double? old = Best(level);
        if (old != null && old <= seconds)
            return false;

        Preferences.Default.Set("best-" + level, seconds);
        return true;
    }

    // Første bane man ikke har klaret. Er alle klaret, bliver det den sidste.
    public int NextLevel()
    {
        for (int level = 1; level <= LevelCount; level++)
        {
            if (Best(level) == null)
                return level;
        }
        return LevelCount;
    }

    // 75,3 sekunder bliver til "1:15.3"
    public static string FormatTime(double seconds)
    {
        int minutes = (int)(seconds / 60);
        double rest = seconds - minutes * 60;
        return minutes + ":" + rest.ToString("00.0", CultureInfo.InvariantCulture);
    }
}
