namespace WorldEngine.Domain.Entities;

public class AgentInventory
{
    public Guid AgentId { get; set; }

    public string ResourceType { get; set; } = string.Empty;

    public double Quantity { get; set; }

    public DateTime UpdatedAt { get; set; }
}