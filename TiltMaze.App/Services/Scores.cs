using TiltMaze.Core;

namespace TiltMaze.App.Services;

// Bedste tider og spøgelser. Tider gemmes lokalt på enheden med Preferences (SQLite kommer senere),
// spøgelser som små filer i appens datamappe.
public class Scores
{
    // Spøgelsets målinger: kuglens x og y hvert 1/30 sekund, som en række af tal (x0, y0, x1, y1, ...)
    public const double GhostRate = 30;

    // Bedste tid på netop denne labyrint (sværhedsgrad + seed), eller null hvis den ikke er klaret endnu.
    // Dagens labyrint er også bare en seed, så den bedste tid i dag er Best(d, Course.DailySeed()).
    public double? Best(Difficulty difficulty, int seed)
    {
        return Read("best-" + difficulty + "-" + seed);
    }

    // Bedste tid på nogen labyrint i sværhedsgraden
    public double? BestOf(Difficulty difficulty)
    {
        return Read("best-" + difficulty);
    }

    // Gemmer tiden, hvis den er bedre end den gamle. Giver true, hvis det er en ny rekord på labyrinten.
    public bool Save(Difficulty difficulty, int seed, double seconds)
    {
        double? bestOf = BestOf(difficulty);
        if (bestOf == null || seconds < bestOf)
            Preferences.Default.Set("best-" + difficulty, seconds);

        double? old = Best(difficulty, seed);
        if (old != null && old <= seconds)
            return false;

        Preferences.Default.Set("best-" + difficulty + "-" + seed, seconds);
        return true;
    }

    public float[]? LoadGhost(Difficulty difficulty, int seed)
    {
        string path = GhostPath(difficulty, seed);
        if (!File.Exists(path))
            return null;

        byte[] bytes = File.ReadAllBytes(path);
        var samples = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * sizeof(float));
        return samples;
    }

    public void SaveGhost(Difficulty difficulty, int seed, float[] samples)
    {
        var bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        File.WriteAllBytes(GhostPath(difficulty, seed), bytes);
    }

    static string GhostPath(Difficulty difficulty, int seed)
    {
        return Path.Combine(FileSystem.AppDataDirectory, "ghost-" + difficulty + "-" + seed + ".bin");
    }

    static double? Read(string key)
    {
        double seconds = Preferences.Default.Get(key, 0.0);
        if (seconds > 0)
            return seconds;
        return null;
    }
}
