namespace WorldEngine.Domain.Entities;

public class LocationResource
{
    public Guid LocationId { get; set; }

    public string ResourceType { get; set; } = string.Empty;

    public double Quantity { get; set; }

    public DateTime UpdatedAt { get; set; }
}