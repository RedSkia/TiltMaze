namespace TiltMaze.Core;

// Kuglens fysik. Alt regnes i celler (1 celle = 1 enhed) og sekunder.
public class Ball
{
    public const double Radius = 0.26;          // kuglens radius
    public const double WallHalfWidth = 0.04;   // halv tykkelse på en væg
    public const double MaxSpeed = 5;           // celler pr. sekund
    public const double Step = 1.0 / 120;       // fast tidsskridt
    const double Accel = 14;                    // fart pr. sekund ved fuld vip
    const double Friction = 2;                  // bremser kuglen
    const double Bounce = 0.3;                  // 0 = ingen, 1 = fuld

    public double X, Y, VX, VY;

    public double Speed => Math.Sqrt(VX * VX + VY * VY);

    readonly List<Segment> walls;
    double leftover;   // tid der endnu ikke er brugt på et helt tidsskridt

    public Ball(List<Segment> walls, double x, double y)
    {
        this.walls = walls;
        X = x;
        Y = y;
    }

    // Sæt kuglen et sted og stands den (fx start igen efter et hul)
    public void Reset(double x, double y)
    {
        X = x;
        Y = y;
        VX = 0;
        VY = 0;
        leftover = 0;
    }

    // Kør fysikken "seconds" frem. ax og ay er vip fra -1 til 1. Tiden deles op i faste små skridt,
    // så kuglen aldrig flytter sig længere pr. skridt, end en væg er tyk (ingen tunnelering).
    // "stop" kaldes efter hvert skridt (fx tjek af hul og mål). Giver den true, stopper vi med det samme.
    public void Update(double seconds, double ax, double ay, Func<bool>? stop = null)
    {
        leftover += seconds;
        while (leftover >= Step)
        {
            leftover -= Step;
            OneStep(ax, ay);
            if (stop != null && stop()) return;
        }
    }

    void OneStep(double ax, double ay)
    {
        // Fart fra vip, minus friktion
        VX += (ax * Accel - VX * Friction) * Step;
        VY += (ay * Accel - VY * Friction) * Step;

        // Maks-fart
        double speed = Speed;
        if (speed > MaxSpeed)
        {
            VX = VX / speed * MaxSpeed;
            VY = VY / speed * MaxSpeed;
        }

        X += VX * Step;
        Y += VY * Step;

        foreach (Segment wall in walls)
            Collide(wall);
    }

    // Skubber kuglen ud af væggen, hvis den er inde i den, og lader den prelle af
    void Collide(Segment wall)
    {
        // Nærmeste punkt på linjestykket
        double dx = wall.X2 - wall.X1;
        double dy = wall.Y2 - wall.Y1;
        double t = ((X - wall.X1) * dx + (Y - wall.Y1) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0, 1);
        double nearX = wall.X1 + t * dx;
        double nearY = wall.Y1 + t * dy;

        // Afstand fra kuglens midte til det punkt
        double awayX = X - nearX;
        double awayY = Y - nearY;
        double distance = Math.Sqrt(awayX * awayX + awayY * awayY);
        double minDistance = Radius + WallHalfWidth;
        if (distance >= minDistance || distance == 0) return;

        // Normal = retning væk fra væggen
        double nx = awayX / distance;
        double ny = awayY / distance;
        X += nx * (minDistance - distance);
        Y += ny * (minDistance - distance);

        // Fjern (og vend lidt om på) den del af farten, der peger ind i væggen
        double into = VX * nx + VY * ny;
        if (into < 0)
        {
            VX -= (1 + Bounce) * into * nx;
            VY -= (1 + Bounce) * into * ny;
        }
    }
}
