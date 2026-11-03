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
}
