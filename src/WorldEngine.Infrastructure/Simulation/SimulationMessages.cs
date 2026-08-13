using WorldEngine.Domain.Enums;

namespace WorldEngine.Infrastructure.Simulation;

public sealed record WorldStateUpdate(
    Guid WorldId,
    long TickNumber,
    DateTime SimulationTime,
    double Speed,
    SimulationStatus Status,
    DateTime UpdatedAt);

public sealed record SimulationEventDto(
    Guid WorldId,
    long Tick,
    DateTime SimulationTime,
    string EventType,
    Guid? ActorAgentId,
    Guid? TargetAgentId,
    Guid? LocationId,
    IReadOnlyDictionary<string, object?> Data);