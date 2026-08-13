namespace WorldEngine.Domain.Entities;

public class Group
{
    public Guid Id { get; set; }

    public Guid WorldId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Status { get; set; } = "Forming";

    public string FormationReason { get; set; } = string.Empty;

    public DateTime FormationSimulationTime { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class GroupMembership
{
    public Guid GroupId { get; set; }

    public Guid AgentId { get; set; }

    public string Role { get; set; } = "Member";

    public DateTime JoinedAt { get; set; }
}