using TiltMaze.Core;

namespace TiltMaze.App.Services;

// Mønter og ting fra butikken. Alt gemmes lokalt i SQLite (GameDatabase); det valgte ligger i Preferences.
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
    public BallColor EquippedColor => Rewards.ColorById(EquippedId(ShopCategory.Color));

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

    // Gemmer stjernerne, hvis det er flere end før
    public void SaveStars(Difficulty difficulty, int seed, int stars)
    {
        var progress = database.GetProgress(difficulty, seed);
        if (stars <= progress.Stars)
            return;

        progress.Stars = stars;
        database.SaveProgress(progress, 0);
    }

    public int Stars(Difficulty difficulty, int seed)
    {
        return database.GetProgress(difficulty, seed).Stars;
    }

    // Streak: dage i træk med en klaret labyrint (UTC), gemt i Preferences
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static DateOnly? LastDay =>
        DateOnly.TryParseExact(Preferences.Get("streakDate", ""), "yyyy-MM-dd", out var day) ? day : null;

    public int Streak => Rewards.ShownStreak(LastDay, Today, Preferences.Get("streak", 0));

    // Kaldes når en labyrint er klaret. Giver mønter, hvis det er en ny dag i stribe (ellers 0).
    public int UpdateStreak()
    {
        if (LastDay == Today)
            return 0;

        Preferences.Set("streak", Rewards.NextStreak(LastDay, Today, Preferences.Get("streak", 0)));
        Preferences.Set("streakDate", Today.ToString("yyyy-MM-dd"));
        var wallet = database.GetWallet();
        wallet.Coins += Rewards.StreakBonus;
        database.SaveWallet(wallet);
        return Rewards.StreakBonus;
    }

    // Kaldes når en labyrint er klaret (efter UpdateStreak). Giver de nye præstationer og deres mønter.
    public List<Achievement> Unlock(Difficulty difficulty, int stars, bool clean)
    {
        int cleanRuns = Preferences.Get("cleanRuns", 0) + (clean ? 1 : 0);
        Preferences.Set("cleanRuns", cleanRuns);
        var wallet = database.GetWallet();
        var owned = wallet.Owned.Split(',').ToList();
        var news = Rewards.Achieved(difficulty, stars, cleanRuns, Streak).Where(a => !owned.Contains("achievement:" + a.Id)).ToList();
        foreach (var a in news)
        {
            owned.Add("achievement:" + a.Id);
            wallet.Coins += a.Coins;
        }
        wallet.Owned = string.Join(",", owned.Where(o => o != ""));
        database.SaveWallet(wallet);
        return news;
    }

    public bool HasAchievement(Achievement a)
    {
        return database.GetWallet().Owned.Split(',').Contains("achievement:" + a.Id);
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

    // Ejede ting ligger i WalletRow.Owned som nøgler med komma imellem (se ShopItems.OwnedKey)
    public bool Owns(ShopItem item)
    {
        return item.Price == 0 || database.GetWallet().Owned.Split(',').Contains(ShopItems.OwnedKey(item));
    }

    // Køber tingen, hvis man har råd og ikke ejer den. Giver true, hvis den blev købt.
    public bool Buy(ShopItem item)
    {
        var wallet = database.GetWallet();
        if (Owns(item) || wallet.Coins < item.Price)
            return false;

        string key = ShopItems.OwnedKey(item);
        wallet.Coins -= item.Price;
        wallet.Owned = wallet.Owned == "" ? key : wallet.Owned + "," + key;
        database.SaveWallet(wallet);
        return true;
    }

    // Vælger tingen. Kun ting man ejer.
    public void Equip(ShopItem item)
    {
        if (Owns(item))
            Preferences.Set(PreferenceName(item.Category), item.Id);
    }

    // Id på den valgte ting i slagsen. Ukendt id giver den gratis.
    public string EquippedId(ShopCategory category)
    {
        return ShopItems.ById(category, Preferences.Get(PreferenceName(category), "")).Id;
    }

    private static string PreferenceName(ShopCategory category)
    {
        return category switch
        {
            ShopCategory.Color => "ballColor",
            ShopCategory.Trail => "trail",
            ShopCategory.Pattern => "pattern",
            ShopCategory.GoalEffect => "goalEffect",
            _ => "theme",
        };
    }
}
