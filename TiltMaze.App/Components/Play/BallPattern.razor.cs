using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace TiltMaze.App.Components.Play;

public partial class BallPattern
{
    [Parameter] public string Pattern { get; set; } = "plain";
    [Parameter] public double X { get; set; }
    [Parameter] public double Y { get; set; }
    [Parameter] public double R { get; set; }

    // Tal til SVG skal have punktum, uanset sprog på computeren
    private static string N(double number)
    {
        return number.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
