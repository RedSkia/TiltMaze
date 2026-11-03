using System.Globalization;

namespace TiltMaze.Core;

public static class Format
{
    // 75,3 sekunder bliver til "1:15.3". Der rundes til 0,1 først, så 59,96 bliver "1:00.0" og ikke "0:60.0".
    public static string Time(double seconds)
    {
        double rounded = Math.Round(seconds, 1);
        int minutes = (int)(rounded / 60);
        double rest = rounded - minutes * 60;
        return minutes + ":" + rest.ToString("00.0", CultureInfo.InvariantCulture);
    }

    // Forskel til spøgelset i sekunder: -1,26 bliver "−1.3" (foran), 0,4 bliver "+0.4" (bagud) og 0 bliver "0.0".
    // Minus er det typografiske tegn (U+2212), så det er lige så bredt som plus.
    public static string Delta(double seconds)
    {
        double rounded = Math.Round(seconds, 1);
        string sign = rounded > 0 ? "+" : rounded < 0 ? "−" : "";
        return sign + Math.Abs(rounded).ToString("0.0", CultureInfo.InvariantCulture);
    }
}
