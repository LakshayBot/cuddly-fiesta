using WorldEngine.Domain.Actions;

namespace WorldEngine.Domain.AI;

public sealed class RuleBasedDecisionEngine : IAgentDecisionEngine
{
    private readonly IActionGenerator _generator;

    public RuleBasedDecisionEngine(IActionGenerator generator)
    {
        _generator = generator;
    }

    public Task<AgentDecision> DecideAsync(AgentDecisionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var proposals = _generator.Generate(context);

        var scored = proposals
            .Select(p =>
            {
                var baseScore = DecisionScoring.BaseScore(p, context);
                var personality = DecisionScoring.PersonalityModifier(p, context);
                return new ScoredAction(
                    ActionId: p.ActionId,
                    ActionType: p.ActionType,
                    Action: p.Action,
                    Score: baseScore + personality,
                    Reasoning: $"base={baseScore:0.##}, personality={personality:0.##}");
            })
            .OrderByDescending(s => s.Score)
            .ToList();

        var valid = scored
            .Where(s => s.Action.IsAvailable(context.ActionContext))
            .ToList();

        ScoredAction? selected;
        string? reasoning;
        if (valid.Count > 0)
        {
            selected = valid[0];
            reasoning = $"Highest valid score. {selected.Reasoning}";
        }
        else
        {
            selected = scored.FirstOrDefault(s => s.ActionType == ActionTypes.Idle);
            reasoning = "No valid action; falling back to Idle.";
        }

        var decision = new AgentDecision(
            agentId: context.Agent.Id,
            decisionSource: AgentDecision.Sources.RuleBased,
            availableActions: scored,
            selectedAction: selected,
            reasoning: reasoning,
            decidedAt: context.Now,
            fallbackUsed: false,
            modelName: null,
            promptVersion: null,
            latencyMs: null);

        return Task.FromResult(decision);
    }
}