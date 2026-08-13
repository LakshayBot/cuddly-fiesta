namespace WorldEngine.Domain.Simulation;

public sealed class SeededRandomSource : IRandomSource
{
    private readonly Random _random;

    public SeededRandomSource(int seed)
    {
        _random = new Random(seed);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentException("maxExclusive must be greater than minInclusive.", nameof(maxExclusive));
        }

        return _random.Next(minInclusive, maxExclusive);
    }

    public double NextDouble() => _random.NextDouble();

    public bool NextBool(double probability = 0.5)
    {
        if (probability < 0.0 || probability > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(probability), "probability must be between 0.0 and 1.0.");
        }

        return _random.NextDouble() < probability;
    }
}