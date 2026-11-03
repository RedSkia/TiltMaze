using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using PointerEventArgs = Microsoft.AspNetCore.Components.Web.PointerEventArgs;   // MAUI har en klasse med samme navn

namespace TiltMaze.App.Components.Play;

public partial class Joystick
{
    [Inject] private IJSRuntime js { get; set; } = null!;

    private const int Size = 140;               // ringens størrelse i pixels
    private const double Reach = Size * 0.32;   // hvor langt knappen kan trækkes fra midten
    private const double DeadZone = 0.12;       // helt små udslag tæller ikke

    // Retningen fra -1 til 1 (X mod højre, Y nedad). Spillet læser dem i hvert billede.
    public double X { get; private set; }
    public double Y { get; private set; }

    private ElementReference ring;
    private bool dragging;
    private double knobX, knobY;   // knappens plads i pixels fra midten

    private async Task Down(PointerEventArgs e)
    {
        dragging = true;
        MoveKnob(e);
        await js.InvokeVoidAsync("tiltMaze.capture", ring, e.PointerId);
    }

    private void Move(PointerEventArgs e)
    {
        if (dragging)
            MoveKnob(e);
    }

    // Slip: knappen springer tilbage til midten
    private void Up()
    {
        dragging = false;
        knobX = 0;
        knobY = 0;
        X = 0;
        Y = 0;
    }

    // OffsetX/Y er fingerens plads i ringen (knappen selv tager ikke imod tryk, se CSS)
    private void MoveKnob(PointerEventArgs e)
    {
        double dx = e.OffsetX - Size / 2.0;
        double dy = e.OffsetY - Size / 2.0;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length > Reach)
        {
            dx = dx / length * Reach;
            dy = dy / length * Reach;
        }

        knobX = dx;
        knobY = dy;
        X = dx / Reach;
        Y = dy / Reach;
        if (Math.Sqrt(X * X + Y * Y) < DeadZone)
        {
            X = 0;
            Y = 0;
        }
    }

    private static string N(double number)
    {
        return number.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
