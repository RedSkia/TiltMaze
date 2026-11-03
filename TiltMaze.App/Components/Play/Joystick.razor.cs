using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using PointerEventArgs = Microsoft.AspNetCore.Components.Web.PointerEventArgs;   // MAUI har en klasse med samme navn

namespace TiltMaze.App.Components.Play;

public partial class Joystick
{
    [Inject] private IJSRuntime js { get; set; } = null!;

    // true mens en dialog er åben: så tager feltet ikke imod tryk
    [Parameter] public bool Disabled { get; set; }

    private const int Size = 140;               // ringens størrelse i pixels
    private const double Reach = Size * 0.32;   // hvor langt knappen kan trækkes fra midten
    private const double DeadZone = 0.12;       // helt små udslag tæller ikke

    // Retningen fra -1 til 1 (X mod højre, Y nedad). Spillet læser dem i hvert billede.
    public double X { get; private set; }
    public double Y { get; private set; }

    private ElementReference zone;
    private long? pointerId;            // den finger (eller mus), der styrer. null = ingen ring
    private double centerX, centerY;    // ringens midte i pixels i feltet
    private double knobX, knobY;        // knappens plads i pixels fra midten

    // Kun den første finger styrer. En finger mere (fx på HUD'en eller ved et uheld) ignoreres
    private async Task Down(PointerEventArgs e)
    {
        if (pointerId != null)
            return;
        pointerId = e.PointerId;
        centerX = e.OffsetX;
        centerY = e.OffsetY;
        MoveKnob(e);
        await js.InvokeVoidAsync("tiltMaze.capture", zone, e.PointerId);
    }

    private void Move(PointerEventArgs e)
    {
        if (e.PointerId == pointerId)
            MoveKnob(e);
    }

    private void Up(PointerEventArgs e)
    {
        if (e.PointerId == pointerId)
            Release();
    }

    // Slip (eller pause): ringen forsvinder, og styringen er 0
    public void Release()
    {
        pointerId = null;
        knobX = 0;
        knobY = 0;
        X = 0;
        Y = 0;
    }

    // OffsetX/Y er fingerens plads i feltet (feltet har pointer capture, og ringen tager ikke imod tryk, se CSS)
    private void MoveKnob(PointerEventArgs e)
    {
        double dx = e.OffsetX - centerX;
        double dy = e.OffsetY - centerY;
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
