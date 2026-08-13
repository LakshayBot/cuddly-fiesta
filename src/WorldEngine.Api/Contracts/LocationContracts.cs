using WorldEngine.Domain.Entities;

namespace WorldEngine.Api.Contracts;

public record LocationResponse(
    Guid Id,
    Guid WorldId,
    string Name,
    string Type,
    IReadOnlyDictionary<string, double> Resources,
    DateTime UpdatedAt);

public record SimulationEventResponse(
    Guid Id,
    long Tick,
    DateTime SimulationTime,
    string EventType,
    Guid? ActorAgentId,
    Guid? TargetAgentId,
    Guid? LocationId,
    string Data,
    DateTime CreatedAt);

public static class LocationMappings
{
    public static LocationResponse ToResponse(this Location location, IReadOnlyDictionary<string, double> resources) =>
        new(
            location.Id,
            location.WorldId,
            location.Name,
            location.Type,
            resources,
            location.UpdatedAt);

    public static SimulationEventResponse ToResponse(this SimulationEvent evt) =>
        new(
            evt.Id,
            evt.Tick,
            evt.SimulationTime,
            evt.EventType,
            evt.ActorAgentId,
            evt.TargetAgentId,
            evt.LocationId,
            evt.Data,
            evt.CreatedAt);
}

public record SettlementResponse(
    Guid Id,
    Guid WorldId,
    string Name,
    string CenterLocationName,
    int Population,
    string Status,
    string FormationReason,
    DateTime FirstPopulationAtTick,
    DateTime CreationSimulationTime,
    DateTime UpdatedAt);

public record GroupResponse(
    Guid Id,
    Guid WorldId,
    string Name,
    string Type,
    string Status,
    string FormationReason,
    DateTime FormationSimulationTime,
    IReadOnlyList<GroupMemberResponse> Members);

public record GroupMemberResponse(Guid AgentId, string Name, string Role, DateTime JoinedAt);