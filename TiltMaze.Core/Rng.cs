namespace TiltMaze.Core;

// Lille seedet tilfældighedsgenerator (mulberry32). System.Random kan ikke genskabes i TypeScript,
// og serveren (TypeScript) skal bygge præcis samme labyrint som appen. Kun heltalsregning på 32 bit.
public class Rng
{
    uint state;

    public Rng(int seed)
    {
        state = (uint)seed;
    }

    public uint NextUInt()
    {
        state += 0x6D2B79F5;
        uint t = state;
        t = (t ^ (t >> 15)) * (t | 1);
        t ^= t + (t ^ (t >> 7)) * (t | 61);
        return t ^ (t >> 14);
    }

    // Et tal fra 0 til og med int.MaxValue (bruges som seed til næste generator)
    public int Next()
    {
        return (int)(NextUInt() >> 1);
    }

    // Et helt tal fra 0 til n - 1
    public int Next(int n)
    {
        return (int)(NextUInt() % (uint)n);
    }

    // Et tal fra 0 (med) til 1 (uden)
    public double NextDouble()
    {
        return NextUInt() / 4294967296.0;
    }
}
