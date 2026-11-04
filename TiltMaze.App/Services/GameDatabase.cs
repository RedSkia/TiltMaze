using SQLite;
using TiltMaze.Core;

namespace TiltMaze.App.Services;

// Bedste tid på en labyrint (sværhedsgrad + seed) med spøgelset der hører til
public class BestRun
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("Maze", 1, Unique = true)]
    public Difficulty Difficulty { get; set; }

    [Indexed("Maze", 2, Unique = true)]
    public int Seed { get; set; }

    public double Seconds { get; set; }
    public byte[]? Ghost { get; set; }   // float32 x,y hvert 1/30 s (se Ghost i Core)
}

// En ny rekord der endnu ikke er sendt til serveren
public class PendingRun
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public Difficulty Difficulty { get; set; }
    public int Seed { get; set; }
    public int TimeMs { get; set; }
    public byte[] Ghost { get; set; } = [];
}

// Den lokale SQLite-database: bedste tider med spøgelser og køen af rekorder der skal sendes
public class GameDatabase
{
    private readonly SQLiteConnection db;

    public GameDatabase()
    {
        db = new SQLiteConnection(Path.Combine(FileSystem.AppDataDirectory, "tiltmaze.db3"));
        db.CreateTable<BestRun>();
        db.CreateTable<PendingRun>();
    }

    // Sletter alle ventende rekorder (når brugeren logger ud)
    public void ClearPending()
    {
        db.DeleteAll<PendingRun>();
    }

    public BestRun? GetBest(Difficulty difficulty, int seed)
    {
        return db.Table<BestRun>().FirstOrDefault(b => b.Difficulty == difficulty && b.Seed == seed);
    }

    // Bedste tid på nogen labyrint i sværhedsgraden
    public double? BestOf(Difficulty difficulty)
    {
        var times = db.Table<BestRun>().Where(b => b.Difficulty == difficulty).ToList();
        if (times.Count == 0)
            return null;
        return times.Min(b => b.Seconds);
    }

    // Ny bedste tid med turen, og rekorden sættes i kø til serveren. Det sker i én transaktion.
    // Kun den nyeste rekord pr. labyrint skal sendes.
    public void SaveBest(Difficulty difficulty, int seed, double seconds, byte[] ghost)
    {
        db.RunInTransaction(() =>
        {
            var best = GetBest(difficulty, seed);
            if (best == null)
                db.Insert(new BestRun { Difficulty = difficulty, Seed = seed, Seconds = seconds, Ghost = ghost });
            else
            {
                best.Seconds = seconds;
                best.Ghost = ghost;
                db.Update(best);
            }

            db.Execute("DELETE FROM PendingRun WHERE Difficulty = ? AND Seed = ?", (int)difficulty, seed);
            db.Insert(new PendingRun { Difficulty = difficulty, Seed = seed, TimeMs = (int)Math.Round(seconds * 1000), Ghost = ghost });
        });
    }

    public List<PendingRun> GetPending()
    {
        return db.Table<PendingRun>().OrderBy(p => p.Id).ToList();
    }

    public void RemovePending(PendingRun run)
    {
        db.Delete(run);
    }
}
