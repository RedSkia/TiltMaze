namespace TiltMaze.Core;

// Kameraet på små skærme: et udsnit af banen (målt i celler), der glider blødt efter kuglen
// og aldrig viser noget uden for banen. Er udsnittet større end banen, står kameraet bare i midten.
public class Camera
{
    public const double Margin = 0.15;   // lidt luft om banen, så den yderste væg kan ses helt
    const double Speed = 8;              // hvor hurtigt kameraet indhenter kuglen (større = hurtigere)

    readonly double mazeWidth;
    readonly double mazeHeight;

    // Midten af udsnittet og dets størrelse, alt i celler
    public double X { get; private set; }
    public double Y { get; private set; }
    public double ViewWidth { get; private set; }
    public double ViewHeight { get; private set; }

    public double Left => X - ViewWidth / 2;
    public double Top => Y - ViewHeight / 2;

    public Camera(double mazeWidth, double mazeHeight)
    {
        this.mazeWidth = mazeWidth;
        this.mazeHeight = mazeHeight;
        SetView(mazeWidth + Margin * 2, mazeHeight + Margin * 2);
        Snap(mazeWidth / 2, mazeHeight / 2);
    }

    // Udsnittets størrelse i celler (skærmens størrelse delt med pixels pr. celle)
    public void SetView(double width, double height)
    {
        ViewWidth = width;
        ViewHeight = height;
        X = Clamp(X, ViewWidth, mazeWidth);
        Y = Clamp(Y, ViewHeight, mazeHeight);
    }

    // Hop direkte til punktet, fx når en ny labyrint starter
    public void Snap(double x, double y)
    {
        X = Clamp(x, ViewWidth, mazeWidth);
        Y = Clamp(y, ViewHeight, mazeHeight);
    }

    // Glid et stykke mod punktet. Samme brøkdel af afstanden pr. tid, så det føles blødt uanset billedhastighed.
    public void Follow(double x, double y, double seconds)
    {
        double part = 1 - Math.Exp(-seconds * Speed);
        X += (Clamp(x, ViewWidth, mazeWidth) - X) * part;
        Y += (Clamp(y, ViewHeight, mazeHeight) - Y) * part;
    }

    // Holder udsnittets kanter inden for banen (plus margin). Passer hele banen i udsnittet, står midten i midten.
    static double Clamp(double center, double view, double size)
    {
        double min = view / 2 - Margin;
        double max = size + Margin - view / 2;
        if (min >= max)
            return size / 2;
        return Math.Clamp(center, min, max);
    }
}
