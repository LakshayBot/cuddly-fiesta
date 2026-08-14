using WorldEngine.Domain.Actions;

namespace WorldEngine.Domain.AI;

public sealed record ScoredAction(
    string ActionId,
    string ActionType,
    IAgentAction Action,
    double Score,
    string? Reasoning,
    IReadOnlyList<DecisionFactor>? Factors = null)
{
    public string Describe() => ActionType;
}

public sealed class AgentDecision
{
    public AgentDecision(
        Guid agentId,
        string decisionSource,
        IReadOnlyList<ScoredAction> availableActions,
        ScoredAction? selectedAction,
        string? reasoning,
        DateTime decidedAt,
        bool fallbackUsed = false,
        string? modelName = null,
        string? promptVersion = null,
        int? latencyMs = null)
    {
        AgentId = agentId;
        DecisionSource = decisionSource;
        AvailableActions = availableActions;
        SelectedAction = selectedAction;
        Reasoning = reasoning;
        DecidedAt = decidedAt;
        FallbackUsed = fallbackUsed;
        ModelName = modelName;
        PromptVersion = promptVersion;
        LatencyMs = latencyMs;
    }

    public Guid AgentId { get; }

    public string DecisionSource { get; }

    public IReadOnlyList<ScoredAction> AvailableActions { get; }

    public ScoredAction? SelectedAction { get; }

    public string? Reasoning { get; }

    public DateTime DecidedAt { get; }

    public bool FallbackUsed { get; }

    public string? ModelName { get; }

    public string? PromptVersion { get; }

    public int? LatencyMs { get; }

    public static class Sources
    {
        public const string RuleBased = "RuleBased";
        public const string RuleBasedFallback = "RuleBased:Fallback";
        public const string LlmPrefix = "LLM";
    }
}