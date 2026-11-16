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
    public string? Username { get; set; }   // hvem rekorden er sat som (tom eller null = gæst)
}

// Hvad der er tjent på en labyrint (sværhedsgrad + seed), så hver ting kun giver mønter én gang
public class MazeProgress
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("Maze", 1, Unique = true)]
    public Difficulty Difficulty { get; set; }

    [Indexed("Maze", 2, Unique = true)]
    public int Seed { get; set; }

    public int CoinMask { get; set; }     // bit i er 1, hvis mønt nummer i er samlet op
    public bool Completed { get; set; }   // belønningen for at klare labyrinten er givet
    public int RankBonusPaid { get; set; }   // 0, 25 eller 50 (se Rewards)
    public int Stars { get; set; }           // flest stjerner (0 = ikke klaret). CreateTable tilføjer kolonnen i gamle databaser.
}

// Pungen: antal mønter og de ting man ejer (nøgler med komma imellem, se ShopItems.OwnedKey; farver er bare id'et).
// Der er altid kun én række, med Id 1.
public class WalletRow
{
    [PrimaryKey]
    public int Id { get; set; }

    public int Coins { get; set; }
    public string Owned { get; set; } = "";
}

// Den lokale SQLite-database: bedste tider med spøgelser, køen af rekorder der skal sendes, og mønter
public class GameDatabase
{
    private readonly SQLiteConnection db;

    public GameDatabase()
    {
        db = new SQLiteConnection(Path.Combine(FileSystem.AppDataDirectory, "tiltmaze.db3"));
        db.CreateTable<BestRun>();
        db.CreateTable<PendingRun>();
        db.CreateTable<MazeProgress>();
        db.CreateTable<WalletRow>();
    }

    // Fremgangen på labyrinten (en tom, hvis der ikke er nogen endnu)
    public MazeProgress GetProgress(Difficulty difficulty, int seed)
    {
        var progress = db.Table<MazeProgress>().FirstOrDefault(p => p.Difficulty == difficulty && p.Seed == seed);
        return progress ?? new MazeProgress { Difficulty = difficulty, Seed = seed };
    }

    // Gemmer fremgangen og lægger de tjente mønter til pungen i én transaktion
    public void SaveProgress(MazeProgress progress, int earned)
    {
        db.RunInTransaction(() =>
        {
            if (progress.Id == 0)
                db.Insert(progress);
            else
                db.Update(progress);
            var wallet = GetWallet();
            wallet.Coins += earned;
            db.InsertOrReplace(wallet);
        });
    }

    public WalletRow GetWallet()
    {
        return db.Find<WalletRow>(1) ?? new WalletRow { Id = 1 };
    }

    public void SaveWallet(WalletRow wallet)
    {
        db.InsertOrReplace(wallet);
    }

    // Sletter alle ventende rekorder (når brugeren logger ud)
    public void ClearPending()
    {
        db.DeleteAll<PendingRun>();
    }

    // Brugeren har skiftet navn: ventende rekorder under det gamle navn hører stadig til brugeren
    public void RenamePending(string oldName, string newName)
    {
        db.Execute("UPDATE PendingRun SET Username = ? WHERE Username = ? COLLATE NOCASE", newName, oldName);
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
    public void SaveBest(Difficulty difficulty, int seed, double seconds, byte[] ghost, string username)
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
            db.Insert(new PendingRun { Difficulty = difficulty, Seed = seed, TimeMs = (int)Math.Round(seconds * 1000), Ghost = ghost, Username = username });
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
