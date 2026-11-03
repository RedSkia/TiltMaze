namespace TiltMaze.Core;

// Et optaget løb (spøgelset): kuglens x og y med fast takt, gemt som tal x0, y0, x1, y1, ...
// Kan sige, hvor spøgelset er på et tidspunkt, og hvornår det første gang kom ind i en celle (til mellemtider).
public class Ghost
{
    readonly float[] samples;
    readonly double rate;          // målinger pr. sekund
    readonly double[,] firstVisit; // tid for første besøg i hver celle, -1 = aldrig

    public Ghost(float[] samples, double rate, int width, int height)
    {
        this.samples = samples;
        this.rate = rate;

        firstVisit = new double[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                firstVisit[x, y] = -1;

        for (int i = 0; i + 1 < samples.Length; i += 2)
        {
            int cellX = (int)samples[i];
            int cellY = (int)samples[i + 1];
            if (cellX < 0 || cellY < 0 || cellX >= width || cellY >= height)
                continue;
            if (firstVisit[cellX, cellY] < 0)
                firstVisit[cellX, cellY] = i / 2 / rate;
        }
    }

    // Hvor spøgelset er efter 'time' sekunder. Det står mellem to målinger, så det glider jævnt.
    // Giver false, når optagelsen er slut.
    public bool TryGetPosition(double time, out double x, out double y)
    {
        x = 0;
        y = 0;
        double position = time * rate;
        int i = (int)position;
        if (time < 0 || (i + 1) * 2 + 1 >= samples.Length)
            return false;

        double part = position - i;
        x = samples[i * 2] + (samples[i * 2 + 2] - samples[i * 2]) * part;
        y = samples[i * 2 + 1] + (samples[i * 2 + 3] - samples[i * 2 + 1]) * part;
        return true;
    }

    // Hvornår spøgelset første gang var i cellen, eller null hvis det aldrig var der
    public double? FirstVisit(int cellX, int cellY)
    {
        if (cellX < 0 || cellY < 0 || cellX >= firstVisit.GetLength(0) || cellY >= firstVisit.GetLength(1))
            return null;
        double time = firstVisit[cellX, cellY];
        if (time < 0)
            return null;
        return time;
    }
}
