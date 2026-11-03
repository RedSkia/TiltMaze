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

    // Ny bedste tid. Spøgelset til den gamle tid passer ikke mere og fjernes
    public void SaveBest(Difficulty difficulty, int seed, double seconds)
    {
        var best = GetBest(difficulty, seed);
        if (best == null)
        {
            db.Insert(new BestRun { Difficulty = difficulty, Seed = seed, Seconds = seconds });
            return;
        }
        best.Seconds = seconds;
        best.Ghost = null;
        db.Update(best);
    }

    public void SaveGhost(Difficulty difficulty, int seed, byte[] ghost)
    {
        var best = GetBest(difficulty, seed);
        if (best == null)
            return;
        best.Ghost = ghost;
        db.Update(best);
    }

    // Kun den nyeste rekord pr. labyrint skal sendes
    public void AddPending(PendingRun run)
    {
        db.Execute("DELETE FROM PendingRun WHERE Difficulty = ? AND Seed = ?", (int)run.Difficulty, run.Seed);
        db.Insert(run);
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
