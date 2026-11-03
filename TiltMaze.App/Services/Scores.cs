using TiltMaze.Core;

namespace TiltMaze.App.Services;

// Bedste tider og spøgelser, som siderne bruger. Alt gemmes i SQLite (GameDatabase), så spillet virker uden net.
// Nye rekorder lægges også i køen, som RunQueue sender til serveren, når der er forbindelse.
public class Scores
{
	// Spøgelsets målinger: kuglens x og y hvert 1/30 sekund, som en række af tal (x0, y0, x1, y1, ...)
	public const double GhostRate = Ghost.Rate;

	private readonly GameDatabase database;
	private readonly RunQueue queue;

	public Scores(GameDatabase database, RunQueue queue)
	{
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

	// Gemmer tiden, hvis den er bedre end den gamle. Giver true, hvis det er en ny rekord på labyrinten.
	// Ved en ny rekord kaldes SaveGhost bagefter med turen.
	public bool Save(Difficulty difficulty, int seed, double seconds)
	{
		double? old = Best(difficulty, seed);
		if (old != null && old <= seconds)
			return false;

		database.SaveBest(difficulty, seed, seconds);
		return true;
	}

	public float[]? LoadGhost(Difficulty difficulty, int seed)
	{
		byte[]? bytes = database.GetBest(difficulty, seed)?.Ghost;
		if (bytes == null)
			return null;
		return Ghost.FromBytes(bytes);
	}

	// Gemmer spøgelset til den nye rekord og sender rekorden til serveren (nu eller senere)
	public void SaveGhost(Difficulty difficulty, int seed, float[] samples)
	{
		BestRun? best = database.GetBest(difficulty, seed);
		if (best == null)
			return;

		byte[] bytes = Ghost.ToBytes(samples);
		database.SaveGhost(difficulty, seed, bytes);
		database.AddPending(new PendingRun
		{
			Difficulty = difficulty,
			Seed = seed,
			TimeMs = (int)Math.Round(best.Seconds * 1000),
			Ghost = bytes
		});
		_ = queue.SendPending();
	}
}
