namespace TiltMaze.App.Services;

// Læser accelerometeret og laver det om til vip: X mod højre og Y nedad på skærmen, begge fra -1 til 1.
// På en computer uden sensor er IsSupported false, og X og Y bliver 0.
public class TiltSensor
{
    public double X { get; private set; }
    public double Y { get; private set; }

    public bool IsSupported => Accelerometer.Default.IsSupported;

    // Styring: vip ELLER joystick, aldrig begge. Gemmes på enheden (standard: vip, hvis telefonen har sensoren)
    public bool Enabled
    {
        get => IsSupported && Preferences.Default.Get("tilt", true);
        set => Preferences.Default.Set("tilt", value);
    }

    DisplayRotation rotation;   // gemmes, så vi ikke spørger skærmen ved hver måling

    public void Start()
    {
        if (!Enabled || Accelerometer.Default.IsMonitoring)
            return;

        rotation = DeviceDisplay.Current.MainDisplayInfo.Rotation;
        DeviceDisplay.Current.MainDisplayInfoChanged += OnDisplayChanged;
        Accelerometer.Default.ReadingChanged += OnReading;
        Accelerometer.Default.Start(SensorSpeed.Game);
    }

    public void Stop()
    {
        if (!IsSupported || !Accelerometer.Default.IsMonitoring)
            return;

        Accelerometer.Default.Stop();
        Accelerometer.Default.ReadingChanged -= OnReading;
        DeviceDisplay.Current.MainDisplayInfoChanged -= OnDisplayChanged;
        X = 0;
        Y = 0;
    }

    void OnDisplayChanged(object? sender, DisplayInfoChangedEventArgs e)
    {
        rotation = e.DisplayInfo.Rotation;
    }

    // Sensoren giver kraften på telefonen i g langs enhedens akser (X mod højre, Y mod toppen).
    // På Android peger målingen væk fra tyngdekraften (+1 g på Z, når telefonen ligger fladt),
    // så fortegnet vendes til sidst. Når skærmen drejes til landskab, bytter akserne plads.
    void OnReading(object? sender, AccelerometerChangedEventArgs e)
    {
        double x = e.Reading.Acceleration.X;
        double y = e.Reading.Acceleration.Y;

        double screenX;
        double screenY;
        switch (rotation)
        {
            case DisplayRotation.Rotation90:
                screenX = -y;
                screenY = -x;
                break;
            case DisplayRotation.Rotation180:
                screenX = -x;
                screenY = y;
                break;
            case DisplayRotation.Rotation270:
                screenX = y;
                screenY = x;
                break;
            default:
                screenX = x;
                screenY = -y;
                break;
        }

        // 0,4 g (ca. 24 graders) giver fuld vip. Helt små udslag ignoreres.
        X = ToTilt(-screenX);
        Y = ToTilt(-screenY);
    }

    static double ToTilt(double g)
    {
        if (Math.Abs(g) < 0.03)
            return 0;
        return Math.Clamp(g / 0.4, -1, 1);
    }
}
