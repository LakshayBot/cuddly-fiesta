using WorldEngine.Domain.Entities;

namespace WorldEngine.Domain.AI;

public static class SignificantSituationDetector
{
    public const double StarvationHungerThreshold = 0.85;

    public const double StrongAffectionThreshold = 0.7;

    public const double HighAngerThreshold = 0.6;

    public const double CriticalHealthThreshold = 0.4;

    public const double HighSocialNeedThreshold = 0.8;

    public const double HighHungerAndSocialThreshold = 0.7;

    public static bool IsSignificant(AgentDecisionContext context)
    {
        var agent = context.Agent;

        if (agent.Health <= CriticalHealthThreshold
            && context.NearbyAgents.Any(a => a.Alive))
        {
            return true;
        }

        if (agent.Hunger >= HighHungerAndSocialThreshold
            && agent.SocialNeed >= HighSocialNeedThreshold)
        {
            return true;
        }

        foreach (var target in context.NearbyAgents)
        {
            if (!target.Alive) continue;

            var rel = context.FindRelationshipWith(target.Id);
            if (rel is null) continue;

            if (rel.Anger >= HighAngerThreshold)
            {
                return true;
            }

            if (target.Hunger >= StarvationHungerThreshold
                && rel.Affection >= StrongAffectionThreshold
                && context.GetInventoryQuantity(ResourceTypes.Food) >= 2.0)
            {
                return true;
            }
        }

        return false;
    }
}