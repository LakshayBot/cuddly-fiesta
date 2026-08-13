namespace WorldEngine.Domain.Entities;

public class AgentMemory
{
    public Guid Id { get; set; }

    public Guid AgentId { get; set; }

    public Guid SimulationEventId { get; set; }

    public string Type { get; set; } = string.Empty;

    public double Importance { get; set; }

    public double EmotionalImpact { get; set; }

    public DateTime CreatedSimulationTime { get; set; }

    public Guid? OtherAgentId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}