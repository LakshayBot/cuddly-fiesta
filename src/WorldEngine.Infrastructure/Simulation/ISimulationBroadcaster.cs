namespace WorldEngine.Infrastructure.Simulation;

public interface ISimulationBroadcaster
{
    Task BroadcastWorldStateAsync(WorldStateUpdate update, CancellationToken cancellationToken);

    Task BroadcastEventAsync(SimulationEventDto evt, CancellationToken cancellationToken);
}