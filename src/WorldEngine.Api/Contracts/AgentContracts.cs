using WorldEngine.Domain.Entities;

namespace WorldEngine.Api.Contracts;

public record AgentResponse(
    Guid Id,
    Guid WorldId,
    string Name,
    double AgeYears,
    DateTime BirthSimulationTime,
    bool Alive,
    DateTime? DeathSimulationTime,
    string? DeathCause,
    string Location,
    string Occupation,
    decimal Money,
    AgentNeedsDto Needs,
    AgentPersonalityDto Personality,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record AgentNeedsDto(
    double Hunger,
    double Energy,
    double Health,
    double Happiness,
    double Safety,
    double SocialNeed);

public record AgentPersonalityDto(
    double Curiosity,
    double Aggression,
    double Empathy,
    double Sociability,
    double Ambition,
    double RiskTolerance,
    double Discipline,
    double Generosity);

public static class AgentMappings
{
    public static AgentResponse ToResponse(this Agent agent, DateTime currentSimulationTime) =>
        new(
            agent.Id,
            agent.WorldId,
            agent.Name,
            Math.Round(agent.GetAgeYears(currentSimulationTime), 3),
            agent.BirthSimulationTime,
            agent.Alive,
            agent.DeathSimulationTime,
            agent.DeathCause,
            agent.Location,
            agent.Occupation,
            agent.Money,
            new AgentNeedsDto(
                agent.Hunger,
                agent.Energy,
                agent.Health,
                agent.Happiness,
                agent.Safety,
                agent.SocialNeed),
            new AgentPersonalityDto(
                agent.Curiosity,
                agent.Aggression,
                agent.Empathy,
                agent.Sociability,
                agent.Ambition,
                agent.RiskTolerance,
                agent.Discipline,
                agent.Generosity),
            agent.CreatedAt,
            agent.UpdatedAt);
}

public record RelationshipResponse(
    Guid SourceAgentId,
    Guid TargetAgentId,
    string? TargetName,
    double Trust,
    double Affection,
    double Respect,
    double Fear,
    double Anger,
    double Familiarity,
    DateTime UpdatedAt);

public record MemoryResponse(
    Guid Id,
    Guid AgentId,
    Guid SimulationEventId,
    string Type,
    double Importance,
    double EmotionalImpact,
    DateTime CreatedSimulationTime,
    Guid? OtherAgentId,
    string Summary,
    DateTime CreatedAt);

public record DecisionResponse(
    Guid Id,
    Guid AgentId,
    long Tick,
    DateTime SimulationTime,
    string DecisionSource,
    string SelectedActionId,
    string SelectedActionType,
    double SelectedScore,
    IReadOnlyList<DecisionActionScore> AvailableActions,
    string? Reasoning,
    DateTime DecidedAt,
    string? ModelName,
    string? PromptVersion,
    int? LatencyMs,
    bool FallbackUsed);

public record DecisionActionScore(string Id, string Type, double Score, string? Reasoning);