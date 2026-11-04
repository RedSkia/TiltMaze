using TiltMaze.Core;

namespace TiltMaze.App.Services;

// Bedste tider og spøgelser, som siderne bruger. Alt gemmes i SQLite (GameDatabase), så spillet virker uden net.
// Nye rekorder lægges også i køen, som RunQueue sender til serveren, når der er forbindelse.
public class Scores
{
    private readonly GameDatabase database;
    private readonly RunQueue queue;
    private readonly Auth auth;

    public Scores(GameDatabase database, RunQueue queue, Auth auth)
    {
        this.auth = auth;
        this.database = database;
        this.queue = queue;
    }

    // Bedste tid på netop denne labyrint (sværhedsgrad + seed), eller null hvis den ikke er klaret endnu.
    // Dagens labyrint er også bare en seed, så den bedste tid i dag er Best(d, Course.DailySeed()).
    public double? Best(Difficulty difficulty, int seed)
    {
        return database.GetBest(difficulty, seed)?.Seconds;
    }

    // Bedste tid på nogen labyrint i sværhedsgraden
    public double? BestOf(Difficulty difficulty)
    {
        return database.BestOf(difficulty);
    }

    public float[]? LoadGhost(Difficulty difficulty, int seed)
    {
        byte[]? bytes = database.GetBest(difficulty, seed)?.Ghost;
        if (bytes == null)
            return null;
        return Ghost.FromBytes(bytes);
    }

    // Gemmer tiden og turen (spøgelset), hvis tiden er bedre end den gamle, og sætter rekorden i kø til serveren.
    // Giver true, hvis det er en ny rekord på labyrinten.
    public bool Save(Difficulty difficulty, int seed, double seconds, float[] samples)
    {
        double? old = Best(difficulty, seed);
        if (old != null && old <= seconds)
            return false;

        database.SaveBest(difficulty, seed, seconds, Ghost.ToBytes(samples), auth.IsLoggedIn ? auth.Username : "");
        _ = queue.SendPending();
        return true;
    }
}
