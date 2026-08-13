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

    public DateTime CreatedAt { get; set; }
}