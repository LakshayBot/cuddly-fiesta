using Microsoft.AspNetCore.SignalR;

namespace WorldEngine.Infrastructure.Simulation;

public sealed class SignalRSimulationBroadcaster : ISimulationBroadcaster
{
    public const string WorldStateUpdatedMethod = "WorldStateUpdated";
    public const string SimulationEventOccurredMethod = "SimulationEventOccurred";

    private readonly IHubContext<SimulationHub> _hubContext;

    public SignalRSimulationBroadcaster(IHubContext<SimulationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastWorldStateAsync(WorldStateUpdate update, CancellationToken cancellationToken)
        => _hubContext.Clients.All.SendAsync(WorldStateUpdatedMethod, update, cancellationToken);

    public Task BroadcastEventAsync(SimulationEventDto evt, CancellationToken cancellationToken)
        => _hubContext.Clients.All.SendAsync(SimulationEventOccurredMethod, evt, cancellationToken);
}