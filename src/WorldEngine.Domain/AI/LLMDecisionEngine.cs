using System.Text.Json;
using WorldEngine.Domain.Actions;
using WorldEngine.Domain.Simulation;

namespace WorldEngine.Domain.AI;

public sealed class LLMDecisionEngine : IAgentDecisionEngine
{
    private readonly IActionGenerator _generator;
    private readonly ILLMClient _client;
    private readonly RuleBasedDecisionEngine _fallback;
    private readonly string _modelName;
    private readonly string _promptVersion;
    private readonly Action<string>? _logger;

    public LLMDecisionEngine(
        IActionGenerator generator,
        ILLMClient client,
        RuleBasedDecisionEngine fallback,
        string modelName = "mock-llm",
        string promptVersion = "v1",
        Action<string>? logger = null)
    {
        _generator = generator;
        _client = client;
        _fallback = fallback;
        _modelName = modelName;
        _promptVersion = promptVersion;
        _logger = logger;
    }

    public async Task<AgentDecision> DecideAsync(AgentDecisionContext context, CancellationToken cancellationToken)
    {
        if (!SignificantSituationDetector.IsSignificant(context))
        {
            return await _fallback.DecideAsync(context, cancellationToken);
        }

        var proposals = _generator.Generate(context);
        var rankedFallback = proposals
            .Select(p => new ScoredAction(
                ActionId: p.ActionId,
                ActionType: p.ActionType,
                Action: p.Action,
                Score: DecisionScoring.BaseScore(p, context) + DecisionScoring.PersonalityModifier(p, context),
                Reasoning: $"base={DecisionScoring.BaseScore(p, context):0.##}+personality={DecisionScoring.PersonalityModifier(p, context):0.##}"))
            .OrderByDescending(s => s.Score)
            .ToList();

        var promptRequest = LlmPromptBuilder.Build(context, proposals);

        LlmPromptResponse response;
        try
        {
            response = await _client.CompleteAsync(promptRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.Invoke($"LLM call failed: {ex.Message}; falling back to rule-based.");
            return await Fallback(context, rankedFallback, fallbackReason: "LLM exception", cancellationToken);
        }

        if (!TryParseLlmResponse(response.ResponseText, out var parsedActionId))
        {
            _logger?.Invoke($"LLM response unparseable or invalid action; falling back to rule-based.");
            return await Fallback(context, rankedFallback, fallbackReason: "LLM response invalid", cancellationToken);
        }

        var match = proposals.FirstOrDefault(p => p.ActionId == parsedActionId);
        if (match is null || !match.Action.IsAvailable(context.ActionContext))
        {
            _logger?.Invoke($"LLM selected '{parsedActionId}' not in valid actions or unavailable; falling back.");
            return await Fallback(context, rankedFallback, fallbackReason: "LLM selected invalid action", cancellationToken);
        }

        var selected = new ScoredAction(match.ActionId, match.ActionType, match.Action, Score: 100, Reasoning: parsedActionId);

        var llmRanked = proposals
            .Select(p => new ScoredAction(p.ActionId, p.ActionType, p.Action,
                Score: p.ActionId == parsedActionId ? 100 : DecisionScoring.BaseScore(p, context) + DecisionScoring.PersonalityModifier(p, context),
                Reasoning: p.ActionId == parsedActionId ? "Selected by LLM" : null))
            .OrderByDescending(s => s.Score)
            .ToList();

        return new AgentDecision(
            agentId: context.Agent.Id,
            decisionSource: $"{AgentDecision.Sources.LlmPrefix}:{response.ModelName}",
            availableActions: llmRanked,
            selectedAction: selected,
            reasoning: "LLM selected action with model " + response.ModelName,
            decidedAt: context.Now,
            fallbackUsed: false,
            modelName: response.ModelName,
            promptVersion: _promptVersion,
            latencyMs: response.LatencyMs);
    }

    private async Task<AgentDecision> Fallback(
        AgentDecisionContext context,
        IReadOnlyList<ScoredAction> proposals,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        var fallbackDecision = await _fallback.DecideAsync(context, cancellationToken);
        return new AgentDecision(
            agentId: fallbackDecision.AgentId,
            decisionSource: AgentDecision.Sources.RuleBasedFallback,
            availableActions: fallbackDecision.AvailableActions,
            selectedAction: fallbackDecision.SelectedAction,
            reasoning: $"{fallbackReason}; {fallbackDecision.Reasoning}",
            decidedAt: fallbackDecision.DecidedAt,
            fallbackUsed: true,
            modelName: fallbackDecision.ModelName,
            promptVersion: fallbackDecision.PromptVersion,
            latencyMs: fallbackDecision.LatencyMs);
    }

    private static bool TryParseLlmResponse(string responseText, out string? actionId)
    {
        actionId = null;
        if (string.IsNullOrWhiteSpace(responseText)) return false;

        try
        {
            using var doc = JsonDocument.Parse(responseText);
            if (!doc.RootElement.TryGetProperty("actionId", out var actionIdElement))
            {
                return false;
            }
            actionId = actionIdElement.GetString();
            return !string.IsNullOrWhiteSpace(actionId);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}