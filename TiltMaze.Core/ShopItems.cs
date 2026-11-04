namespace TiltMaze.Core;

// De fem slags ting man kan købe i butikken. Alt er kun udseende og ændrer ikke fysik, tid eller spøgelse.
public enum ShopCategory { Color, Trail, Pattern, GoalEffect, Theme }

// En ting i butikken. Pris 0 = gratis (standard i sin slags)
public record ShopItem(ShopCategory Category, string Id, string Name, int Price);

// Alle ting i butikken samlet ét sted. Den første i hver slags er gratis og er standard.
// Kuglens farver ligger i Rewards.BallColors (de har også farvetoner) og kommer med her.
public static class ShopItems
{
    public static readonly ShopItem[] All =
    [
        .. Rewards.BallColors.Select(c => new ShopItem(ShopCategory.Color, c.Id, c.Name, c.Price)),

        new(ShopCategory.Trail, "none", "No trail", 0),
        new(ShopCategory.Trail, "neon", "Neon", 40),
        new(ShopCategory.Trail, "fire", "Fire", 80),
        new(ShopCategory.Trail, "rainbow", "Rainbow", 120),
        new(ShopCategory.Trail, "sparkle", "Sparkle", 150),

        new(ShopCategory.Pattern, "plain", "Plain", 0),
        new(ShopCategory.Pattern, "stripes", "Stripes", 40),
        new(ShopCategory.Pattern, "planet", "Planet", 90),
        new(ShopCategory.Pattern, "eye", "Eye", 110),
        new(ShopCategory.Pattern, "ring", "Neon ring", 140),

        new(ShopCategory.GoalEffect, "confetti", "Confetti", 0),
        new(ShopCategory.GoalEffect, "stars", "Stars", 70),
        new(ShopCategory.GoalEffect, "fireworks", "Fireworks", 100),
        new(ShopCategory.GoalEffect, "coins", "Coin rain", 130),

        new(ShopCategory.Theme, "neon", "Neon", 0),
        new(ShopCategory.Theme, "lava", "Lava", 80),
        new(ShopCategory.Theme, "ice", "Ice", 80),
        new(ShopCategory.Theme, "retro", "Retro", 120),
    ];

    public static IEnumerable<ShopItem> In(ShopCategory category)
    {
        return All.Where(i => i.Category == category);
    }

    // Den gratis ting i slagsen (den første)
    public static ShopItem Default(ShopCategory category)
    {
        return In(category).First();
    }

    // Tingen med dette id i slagsen. Kender vi ikke id'et, bruges den gratis.
    public static ShopItem ById(ShopCategory category, string? id)
    {
        return In(category).FirstOrDefault(i => i.Id == id) ?? Default(category);
    }

    // Navnet i listen over ejede ting i SQLite. Farver har kun id'et, som det altid har haft,
    // så farver man allerede ejer virker stadig. Resten får slagsen foran ("trail:fire").
    public static string OwnedKey(ShopItem item)
    {
        return item.Category == ShopCategory.Color ? item.Id : item.Category.ToString().ToLower() + ":" + item.Id;
    }

    // Farverne til en spor-slags, fra nyeste til ældste punkt. Tom liste = intet spor.
    public static string[] TrailColors(string id)
    {
        return id switch
        {
            "neon" => ["#2de2c8"],
            "fire" => ["#ffe27a", "#ff8a3d", "#ee3b4d", "#7d0c1d"],
            "rainbow" => ["#ff5d73", "#ffa45c", "#ffe27a", "#4ade80", "#38bdf8", "#9a6bff", "#ff5fae"],
            "sparkle" => ["#ffffff", "#fff0b0", "#ffc857", "#8af5e6"],
            _ => [],
        };
    }
}
