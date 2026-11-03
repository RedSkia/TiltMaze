using TiltMaze.Core;

namespace TiltMaze.Api;

// Anti-snyd: tjekker at et spøgelse kan være en rigtig tur på labyrinten (sværhedsgrad + seed).
// Serveren bygger selv labyrinten og bruger tallene fra Core, så appen kan ikke bestemme reglerne.
public static class RunChecker
{
    const double Dt = 1 / Ghost.Rate;
    const double MaxStep = Ball.MaxSpeed * (Dt + 0.03);   // længste skridt mellem to målinger (lidt luft til et langsomt billede)
    const double MaxTotalFactor = 1.02;                   // hele turen må højst være maks-fart x tiden (+ 2 %)
    const double StartTolerance = 0.1;                    // kuglen starter (og starter igen efter et hul) lige ved startcellens midte
    const double WallTolerance = 0.02;                    // luft til at tallene er afrundet til 32 bit
    const double GoalTolerance = MaxStep;                 // sidste måling er taget op til 1/30 s før kuglen nåede målet
    const int MaxSamples = 30 * 60 * 30;                  // højst 30 minutter
    const int MaxSamplesInHole = 9;                       // kuglen falder efter 0,2 s over et hul = 6 målinger (+ luft)

    // Giver null, hvis turen er i orden, ellers en kort grund (kun til log og test, aldrig til brugeren)
    public static string? Check(Course course, int timeMs, float[] ghost)
    {
        if (timeMs < 1 || ghost.Length < 4 || ghost.Length % 2 != 0)
            return "bad length";
        int count = ghost.Length / 2;
        if (count > MaxSamples)
            return "too long";

        // Antal målinger skal passe til tiden (±1 billede)
        long expected = (long)timeMs * 30 / 1000 + 1;
        if (Math.Abs(count - expected) > 1)
            return "time does not match ghost length";

        foreach (float value in ghost)
            if (float.IsNaN(value) || float.IsInfinity(value))
                return "bad number";

        double startX = course.Start.X + 0.5;
        double startY = course.Start.Y + 0.5;
        if (Distance(ghost[0], ghost[1], startX, startY) > StartTolerance)
            return "does not start at the start";

        double total = 0;        // samlet vej
        int samplesInHole = 0;   // hvor mange målinger i træk kuglen har været inde i et hul

        for (int i = 0; i < count; i++)
        {
            double x = ghost[i * 2];
            double y = ghost[i * 2 + 1];

            if (x < 0 || y < 0 || x > course.Width || y > course.Height)
                return "outside the maze";
            if (InsideWall(course, x, y))
                return "inside a wall";

            if (i > 0)
            {
                double px = ghost[i * 2 - 2];
                double py = ghost[i * 2 - 1];
                double step = Distance(x, y, px, py);

                if (step > MaxStep)
                {
                    // Eneste lovlige hop: kuglen faldt i et hul og er tilbage ved start
                    if (Distance(x, y, startX, startY) > StartTolerance || HoleNear(course, px, py) == null)
                        return "moved too far between two points";
                }
                else
                {
                    total += step;
                }

                // Fald: ligger kuglen i et hul uden fart, skal næste måling være tilbage ved start
                Hole? hole = HoleInside(course, x, y);
                if (hole != null && i < count - 1 && step * Ghost.Rate < Course.JumpSpeed * 0.25)
                {
                    double nx = ghost[i * 2 + 2];
                    double ny = ghost[i * 2 + 3];
                    if (Distance(nx, ny, startX, startY) > StartTolerance)
                        return "stopped in a hole";
                }
            }

            if (HoleInside(course, x, y) != null)
            {
                samplesInHole++;
                if (samplesInHole > MaxSamplesInHole)
                    return "too long over a hole";
            }
            else
            {
                samplesInHole = 0;
            }
        }

        // Hele turen må ikke være hurtigere end maks-fart
        double seconds = (count - 1) * Dt;
        if (total > Ball.MaxSpeed * seconds * MaxTotalFactor + 0.5)
            return "faster than the maximum speed";

        // Slutter i målet
        double goalX = course.Goal.X + 0.5;
        double goalY = course.Goal.Y + 0.5;
        if (Distance(ghost[ghost.Length - 2], ghost[ghost.Length - 1], goalX, goalY) > 0.25 + GoalTolerance)
            return "does not end in the goal";

        return null;
    }

    static double Distance(double x1, double y1, double x2, double y2)
    {
        double dx = x1 - x2;
        double dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // Er kuglens midte så tæt på en væg, at den ville være skubbet ud af den?
    static bool InsideWall(Course course, double x, double y)
    {
        double minDistance = Ball.Radius + Ball.WallHalfWidth - WallTolerance;
        foreach (Segment wall in course.Segments)
        {
            double dx = wall.X2 - wall.X1;
            double dy = wall.Y2 - wall.Y1;
            double t = ((x - wall.X1) * dx + (y - wall.Y1) * dy) / (dx * dx + dy * dy);
            t = Math.Clamp(t, 0, 1);
            if (Distance(x, y, wall.X1 + t * dx, wall.Y1 + t * dy) < minDistance)
                return true;
        }
        return false;
    }

    // Hullet kuglens midte er inde i (lidt indenfor kanten, så afrunding ikke giver falsk alarm)
    static Hole? HoleInside(Course course, double x, double y)
    {
        foreach (Hole hole in course.Holes)
            if (Distance(x, y, hole.CenterX, hole.CenterY) < hole.FallRadius - 0.02)
                return hole;
        return null;
    }

    // Et hul kuglen kan være faldet i, når den var her (et skridt fra hullet)
    static Hole? HoleNear(Course course, double x, double y)
    {
        foreach (Hole hole in course.Holes)
            if (Distance(x, y, hole.CenterX, hole.CenterY) < hole.FallRadius + MaxStep)
                return hole;
        return null;
    }
}
