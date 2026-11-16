using System.Text;
using Microsoft.AspNetCore.Components;
using TiltMaze.Core;
using static TiltMaze.Core.Format;

namespace TiltMaze.App.Components.Play;

public partial class MazeLayer
{
    [Parameter, EditorRequired] public Course Maze { get; set; } = null!;

    private Course? drawn;      // labyrinten, der sidst blev tegnet
    private bool changed;
    private string wallPath = "";

    protected override void OnParametersSet()
    {
        changed = Maze != drawn;
        if (changed)
        {
            drawn = Maze;
            wallPath = BuildWallPath();
        }
    }

    // Spillet tegner ca. 60 gange i sekundet. Laget her tegnes kun igen ved en ny labyrint,
    // så Blazor springer alle vægge og huller over i hvert billede.
    protected override bool ShouldRender()
    {
        return changed;
    }

    // Alle vægge i ét SVG-stykke (én tekst), der genbruges af alle fire væg-lag
    private string BuildWallPath()
    {
        var text = new StringBuilder();
        foreach (Segment wall in Maze.Segments)
            text.Append($"M{N(wall.X1)} {N(wall.Y1)}L{N(wall.X2)} {N(wall.Y2)}");
        return text.ToString();
    }
}
