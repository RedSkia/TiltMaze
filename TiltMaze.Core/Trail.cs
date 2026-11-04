namespace TiltMaze.Core;

// Kuglens sidste positioner til sporet bag den. En fast liste, der går rundt (ringbuffer):
// når den er fuld, overskrives den ældste. Ingen nye lister pr. billede.
public class Trail
{
    public const int Length = 14;

    private readonly double[] xs = new double[Length];
    private readonly double[] ys = new double[Length];
    private int next;    // pladsen den næste position skrives på

    public int Count { get; private set; }

    public void Add(double x, double y)
    {
        xs[next] = x;
        ys[next] = y;
        next = (next + 1) % Length;
        if (Count < Length)
            Count++;
    }

    public void Clear()
    {
        Count = 0;
        next = 0;
    }

    // Position nummer i, hvor 0 er den ældste og Count - 1 den nyeste
    public (double X, double Y) At(int i)
    {
        int index = (next - Count + i + Length) % Length;
        return (xs[index], ys[index]);
    }
}
