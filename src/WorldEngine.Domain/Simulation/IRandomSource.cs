namespace WorldEngine.Domain.Simulation;

public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);

    double NextDouble();

    bool NextBool(double probability = 0.5);
}