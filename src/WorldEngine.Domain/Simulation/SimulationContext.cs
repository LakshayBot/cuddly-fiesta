using WorldEngine.Domain.Entities;

namespace WorldEngine.Domain.Simulation;

public sealed class SimulationContext
{
    public SimulationContext(
        World world,
        IRandomSource random,
        DateTime previousSimulationTime,
        DateTime newSimulationTime,
        long tickNumber)
    {
        World = world;
        Random = random;
        PreviousSimulationTime = previousSimulationTime;
        NewSimulationTime = newSimulationTime;
        TickNumber = tickNumber;
    }

    public World World { get; }

    public IRandomSource Random { get; }

    public DateTime PreviousSimulationTime { get; }

    public DateTime NewSimulationTime { get; }

    public long TickNumber { get; }

    public TimeSpan Elapsed => NewSimulationTime - PreviousSimulationTime;
}