namespace WorldEngine.Domain.Entities;

public class AgentDecisionRecord
{
    public Guid Id { get; set; }

    public Guid WorldId { get; set; }

    public Guid AgentId { get; set; }

    public long Tick { get; set; }

    public DateTime SimulationTime { get; set; }

    public string DecisionSource { get; set; } = string.Empty;

    public string SelectedActionId { get; set; } = string.Empty;

    public string SelectedActionType { get; set; } = string.Empty;

    public double SelectedScore { get; set; }

    public string AvailableActionsJson { get; set; } = "[]";

    public string? SelectedFactorsJson { get; set; }

    public string? Reasoning { get; set; }

    public DateTime DecidedAt { get; set; }

    public string? ModelName { get; set; }

    public string? PromptVersion { get; set; }

    public int? LatencyMs { get; set; }

    public bool FallbackUsed { get; set; }
}