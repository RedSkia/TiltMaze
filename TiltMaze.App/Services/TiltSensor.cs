namespace TiltMaze.App.Services;

// Læser accelerometeret og laver det om til vip: X mod højre og Y nedad på skærmen, begge fra -1 til 1.
// På en computer uden sensor er IsSupported false, og X og Y bliver 0.
public class TiltSensor
{
    public double X { get; private set; }
    public double Y { get; private set; }

    public bool IsSupported => Accelerometer.Default.IsSupported;

    public void Start()
    {
        if (!IsSupported || Accelerometer.Default.IsMonitoring)
            return;

        Accelerometer.Default.ReadingChanged += OnReading;
        Accelerometer.Default.Start(SensorSpeed.Game);
    }

    public void Stop()
    {
        if (!IsSupported || !Accelerometer.Default.IsMonitoring)
            return;

        Accelerometer.Default.Stop();
        Accelerometer.Default.ReadingChanged -= OnReading;
        X = 0;
        Y = 0;
    }

    // Sensoren giver tyngdekraften i g langs enhedens akser (X mod højre, Y mod toppen).
    // Når skærmen drejes til landskab, bytter akserne plads.
    void OnReading(object? sender, AccelerometerChangedEventArgs e)
    {
        double x = e.Reading.Acceleration.X;
        double y = e.Reading.Acceleration.Y;
        double screenX;
        double screenY;

        switch (DeviceDisplay.Current.MainDisplayInfo.Rotation)
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
        X = ToTilt(screenX);
        Y = ToTilt(screenY);
    }

    static double ToTilt(double g)
    {
        if (Math.Abs(g) < 0.03)
            return 0;
        return Math.Clamp(g / 0.4, -1, 1);
    }
}
