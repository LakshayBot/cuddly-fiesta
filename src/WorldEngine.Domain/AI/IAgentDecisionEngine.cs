using WorldEngine.Domain.AI;

namespace WorldEngine.Domain.AI;

public interface IAgentDecisionEngine
{
    Task<AgentDecision> DecideAsync(AgentDecisionContext context, CancellationToken cancellationToken);
}