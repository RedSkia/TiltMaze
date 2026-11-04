using TiltMaze.Core;

namespace TiltMaze.App.Services;

// Mønter og kuglens farver. Alt gemmes lokalt i SQLite (GameDatabase); den valgte farve ligger i Preferences.
// Hver ting giver kun mønter én gang pr. labyrint: hver mønt, belønningen for at klare den og top-listebonussen.
public class Wallet
{
    private readonly GameDatabase database;

    public Wallet(GameDatabase database)
    {
        this.database = database;
    }

    public int Coins => database.GetWallet().Coins;

    // Kuglens farve lige nu
    public BallColor Equipped => Rewards.ColorById(Preferences.Get("ballColor", Rewards.BallColors[0].Id));

    // Hvilke mønter på labyrinten der allerede er samlet op (bit i = mønt i)
    public int CoinMask(Difficulty difficulty, int seed)
    {
        return database.GetProgress(difficulty, seed).CoinMask;
    }

    // Kuglen rørte mønt nummer i. Giver true, hvis det var første gang på labyrinten (så giver den en mønt).
    public bool TakeCoin(Difficulty difficulty, int seed, int i)
    {
        var progress = database.GetProgress(difficulty, seed);
        if ((progress.CoinMask >> i & 1) == 1)
            return false;

        progress.CoinMask |= 1 << i;
        database.SaveProgress(progress, Rewards.CoinValue);
        return true;
    }

    // Labyrinten er klaret. Giver antal mønter i belønning, og 0 hvis den er klaret før.
    public int Complete(Difficulty difficulty, int seed)
    {
        var progress = database.GetProgress(difficulty, seed);
        if (progress.Completed)
            return 0;

        progress.Completed = true;
        int reward = Rewards.Completion(difficulty);
        database.SaveProgress(progress, reward);
        return reward;
    }

    // Serveren har svaret med pladsen på top-listen. Giver bonussen, der ikke allerede er givet.
    public int GiveRankBonus(Difficulty difficulty, int seed, int rank)
    {
        var progress = database.GetProgress(difficulty, seed);
        int bonus = Rewards.RankBonusToPay(rank, progress.RankBonusPaid);
        if (bonus == 0)
            return 0;

        progress.RankBonusPaid += bonus;
        database.SaveProgress(progress, bonus);
        return bonus;
    }

    public bool Owns(BallColor color)
    {
        return color.Price == 0 || database.GetWallet().Owned.Split(',').Contains(color.Id);
    }

    // Køber farven, hvis man har råd og ikke ejer den. Giver true, hvis den blev købt.
    public bool Buy(BallColor color)
    {
        var wallet = database.GetWallet();
        if (Owns(color) || wallet.Coins < color.Price)
            return false;

        wallet.Coins -= color.Price;
        wallet.Owned = wallet.Owned == "" ? color.Id : wallet.Owned + "," + color.Id;
        database.SaveWallet(wallet);
        return true;
    }

    // Vælger farven til kuglen. Kun farver man ejer.
    public void Equip(BallColor color)
    {
        if (Owns(color))
            Preferences.Set("ballColor", color.Id);
    }
}
