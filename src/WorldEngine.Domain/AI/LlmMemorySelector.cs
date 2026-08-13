using WorldEngine.Domain.Entities;

namespace WorldEngine.Domain.AI;

public static class LlmMemorySelector
{
    public const int MaxMemories = 10;

    public const double NotableImportanceFloor = 0.4;

    public static IReadOnlyList<AgentMemory> SelectRelevant(
        Agent agent,
        IReadOnlyList<Agent> nearbyAgents,
        IReadOnlyList<AgentMemory> availableMemories)
    {
        var nearbyIds = nearbyAgents.Select(a => a.Id).ToHashSet();
        nearbyIds.Add(agent.Id);

        var scored = availableMemories
            .Select(m => new { Memory = m, Score = Score(m, nearbyIds) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Memory.Importance)
            .ThenByDescending(x => x.Memory.CreatedSimulationTime)
            .Take(MaxMemories)
            .Select(x => x.Memory)
            .ToList();

        if (scored.Count < MaxMemories)
        {
            var fillers = availableMemories
                .Where(m => !scored.Contains(m))
                .OrderByDescending(m => m.Importance)
                .ThenByDescending(m => m.CreatedSimulationTime)
                .Take(MaxMemories - scored.Count);
            scored.AddRange(fillers);
        }

        return scored;
    }

    private static double Score(AgentMemory m, HashSet<Guid> relevantAgentIds)
    {
        double score = 0;

        if (m.OtherAgentId.HasValue && relevantAgentIds.Contains(m.OtherAgentId.Value))
        {
            score += 5;
        }

        score += m.Importance * 3;

        var daysAgo = Math.Max(0, (DateTime.UtcNow - m.CreatedSimulationTime).TotalDays);
        if (daysAgo < 1) score += 2;
        else if (daysAgo < 7) score += 0.5;

        return score;
    }
}