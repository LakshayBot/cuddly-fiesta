namespace WorldEngine.Domain.Entities;

public class AgentRelationship
{
    public Guid SourceAgentId { get; set; }

    public Guid TargetAgentId { get; set; }

    public double Trust { get; set; }

    public double Affection { get; set; }

    public double Respect { get; set; }

    public double Fear { get; set; }

    public double Anger { get; set; }

    public double Familiarity { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}