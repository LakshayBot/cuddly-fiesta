using WorldEngine.Domain.Enums;

namespace WorldEngine.Domain.Entities;

public class World
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int RandomSeed { get; set; }

    public DateTime CurrentSimulationTime { get; set; }

    public double SimulationSpeed { get; set; }

    public SimulationStatus Status { get; set; }

    public long TickNumber { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}