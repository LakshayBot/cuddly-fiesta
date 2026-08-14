using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;

namespace WorldEngine.Api.Contracts;

public record EventCauseResponse(
    Guid Id,
    string CauseType,
    Guid? CauseEventId,
    Guid? DecisionRecordId,
    string Name,
    double? Value,
    string Description,
    long CreatedTick);

public record EventConsequenceResponse(
    Guid Id,
    string Kind,
    string ConsequenceType,
    Guid? ConsequenceEventId,
    Guid? ConsequenceMemoryId,
    string Description,
    long CreatedTick);

public record EventDetailResponse(
    Guid Id,
    long Tick,
    DateTime SimulationTime,
    string EventType,
    Guid? ActorAgentId,
    string? ActorName,
    Guid? TargetAgentId,
    string? TargetName,
    Guid? LocationId,
    string Data,
    EventImportance Importance,
    double ImportanceScore,
    IReadOnlyList<EventCauseResponse> Causes,
    IReadOnlyList<EventConsequenceResponse> DirectConsequences,
    IReadOnlyList<EventConsequenceResponse> IndirectConsequences);

public record LifeMilestoneResponse(
    long Tick,
    DateTime SimulationTime,
    string Type,
    EventImportance Importance,
    string Summary,
    Guid? EventId);

public record AgentLifeResponse(
    Guid AgentId,
    string Name,
    bool Alive,
    double AgeYears,
    string Occupation,
    string Location,
    string? CurrentAction,
    string? CurrentReasoning,
    IReadOnlyList<LifeMilestoneResponse> Milestones);

public record WorldHistoryResponse(
    Guid Id,
    long Tick,
    DateTime SimulationTime,
    string EntryType,
    EventImportance Importance,
    string FactsJson,
    string Summary,
    Guid? RelatedEventId);

public record AutopsyFactorResponse(
    long Tick,
    DateTime SimulationTime,
    string EventType,
    Guid? EventId,
    string Description);

public record AutopsyResponse(
    string Subject,
    string Summary,
    IReadOnlyList<AutopsyFactorResponse> Timeline);