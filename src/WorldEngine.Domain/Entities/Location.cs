namespace WorldEngine.Domain.Entities;

public class Location
{
    public Guid Id { get; set; }

    public Guid WorldId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}