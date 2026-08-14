using WorldEngine.Domain.Enums;

namespace WorldEngine.Domain.Entities;

public class SimulationEvent
{
    public Guid Id { get; set; }

    public Guid WorldId { get; set; }

    public long Tick { get; set; }

    public DateTime SimulationTime { get; set; }

    public string EventType { get; set; } = string.Empty;

    public Guid? ActorAgentId { get; set; }

    public Guid? TargetAgentId { get; set; }

    public Guid? LocationId { get; set; }

    public string Data { get; set; } = "{}";

    public EventImportance Importance { get; set; } = EventImportance.Normal;

    public double ImportanceScore { get; set; }

    public DateTime CreatedAt { get; set; }
}

public static class EventImportanceEvaluator
{
    public static (EventImportance Importance, double Score) Evaluate(
        string eventType,
        int affectedAgentCount,
        double stateMagnitude,
        double relationshipImpact,
        double resourceImpact,
        double populationImpact)
    {
        var score = 0.0;
        score += affectedAgentCount * 0.5;
        score += Math.Clamp(stateMagnitude, 0, 1) * 20;
        score += Math.Clamp(relationshipImpact, 0, 1) * 15;
        score += Math.Clamp(resourceImpact, 0, 1) * 15;
        score += Math.Clamp(populationImpact, 0, 1) * 30;

        var baseScore = eventType switch
        {
            SimulationEventTypes.AgentBorn => 15,
            SimulationEventTypes.AgentDied => 25,
            SimulationEventTypes.AgentAte => 2,
            SimulationEventTypes.AgentRested => 1,
            SimulationEventTypes.AgentMoved => 3,
            SimulationEventTypes.AgentHarvestedFood => 4,
            SimulationEventTypes.AgentGatheredWood => 4,
            SimulationEventTypes.AgentWorked => 4,
            SimulationEventTypes.AgentTalked => 2,
            SimulationEventTypes.AgentHelped => 8,
            SimulationEventTypes.AgentSharedFood => 10,
            SimulationEventTypes.AgentTraded => 8,
            SimulationEventTypes.AgentStole => 12,
            SimulationEventTypes.AgentInsulted => 8,
            SimulationEventTypes.ConflictOccurred => 30,
            SimulationEventTypes.SettlementFormed => 60,
            SimulationEventTypes.GroupFormed => 20,
            _ => 5,
        };
        score += baseScore;

        var importance = score switch
        {
            >= 60 => EventImportance.Historical,
            >= 40 => EventImportance.Major,
            >= 20 => EventImportance.Significant,
            >= 8 => EventImportance.Normal,
            _ => EventImportance.Trivial,
        };

        return (importance, score);
    }
}