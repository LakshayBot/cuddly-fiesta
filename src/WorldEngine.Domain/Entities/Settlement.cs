namespace WorldEngine.Domain.Entities;

public class Settlement
{
    public Guid Id { get; set; }

    public Guid WorldId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CenterLocationName { get; set; } = string.Empty;

    public int Population { get; set; }

    public string Status { get; set; } = "Forming";

    public string FormationReason { get; set; } = string.Empty;

    public DateTime FirstPopulationAtTick { get; set; }

    public DateTime CreationSimulationTime { get; set; }

    public DateTime UpdatedAt { get; set; }
}