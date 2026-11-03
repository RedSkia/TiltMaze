using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace TiltMaze.App.Components.Play;

public partial class RivalGhost
{
    [Parameter] public string Name { get; set; } = "";
    [Parameter] public string Color { get; set; } = "";
    [Parameter] public double X { get; set; }
    [Parameter] public double Y { get; set; }

    // Tal til SVG skal have punktum, uanset sprog på computeren
    private static string N(double number)
    {
        return number.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
