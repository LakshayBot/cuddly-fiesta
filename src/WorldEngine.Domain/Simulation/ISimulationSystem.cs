using WorldEngine.Domain.Entities;

namespace WorldEngine.Domain.Simulation;

public interface ISimulationSystem
{
    Task ProcessAsync(SimulationContext context, CancellationToken cancellationToken);
}