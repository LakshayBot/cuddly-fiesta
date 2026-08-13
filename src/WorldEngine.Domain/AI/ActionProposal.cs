using WorldEngine.Domain.Actions;

namespace WorldEngine.Domain.AI;

public sealed record ActionProposal(
    string ActionId,
    string ActionType,
    IAgentAction Action,
    string Description,
    IReadOnlyDictionary<string, object> Metadata);

public interface IActionGenerator
{
    IReadOnlyList<ActionProposal> Generate(AgentDecisionContext context);
}