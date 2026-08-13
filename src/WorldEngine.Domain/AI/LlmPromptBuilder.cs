using System.Text.Json;
using WorldEngine.Domain.Entities;

namespace WorldEngine.Domain.AI;

public static class LlmPromptBuilder
{
    public const string PromptVersion = "v1";

    public const string DefaultModelName = "mock-llm";

    public static LlmPromptRequest Build(AgentDecisionContext context, IReadOnlyList<ActionProposal> proposals)
    {
        var agent = context.Agent;
        var situation = ClassifySituation(context);

        var systemPrompt = "You are an agent decision system inside a civilization simulation. "
            + "Return JSON with actionId and reason. Do not invent actions; pick only from the provided availableActions.";
        var userPayload = new
        {
            agent = new
            {
                id = agent.Id,
                name = agent.Name,
                age = Math.Round(agent.GetAgeYears(context.Simulation.NewSimulationTime), 2),
                occupation = agent.Occupation,
                personality = new
                {
                    curiosity = Round(agent.Curiosity),
                    aggression = Round(agent.Aggression),
                    empathy = Round(agent.Empathy),
                    sociability = Round(agent.Sociability),
                    ambition = Round(agent.Ambition),
                    riskTolerance = Round(agent.RiskTolerance),
                    discipline = Round(agent.Discipline),
                    generosity = Round(agent.Generosity),
                },
                needs = new
                {
                    hunger = Round(agent.Hunger),
                    energy = Round(agent.Energy),
                    health = Round(agent.Health),
                    happiness = Round(agent.Happiness),
                    safety = Round(agent.Safety),
                    socialNeed = Round(agent.SocialNeed),
                },
                location = agent.Location,
            },
            situation,
            availableActions = proposals.Select(p => new
            {
                id = p.ActionId,
                type = p.ActionType,
                description = p.Description,
            }),
            relevantMemories = context.RecentMemories.Take(8).Select(m => new
            {
                type = m.Type,
                summary = m.Summary,
                importance = Round(m.Importance),
                time = m.CreatedSimulationTime,
            }),
        };

        var userPrompt = JsonSerializer.Serialize(userPayload);
        return new LlmPromptRequest(systemPrompt, userPrompt, DefaultModelName, PromptVersion);
    }

    private static object ClassifySituation(AgentDecisionContext context)
    {
        var agent = context.Agent;
        var starvingNearby = context.NearbyAgents
            .Where(a => a.Alive && a.Hunger >= SignificantSituationDetector.StarvationHungerThreshold)
            .ToList();

        if (agent.Health <= SignificantSituationDetector.CriticalHealthThreshold)
        {
            return new { type = "HealthCritical", health = Round(agent.Health) };
        }

        if (starvingNearby.Count > 0)
        {
            var names = starvingNearby.Select(a => a.Name).ToArray();
            return new { type = "FriendIsStarving", targets = names };
        }

        var angryNearby = context.NearbyAgents
            .Where(a => a.Alive)
            .Where(a =>
            {
                var rel = context.FindRelationshipWith(a.Id);
                return rel is not null && rel.Anger >= SignificantSituationDetector.HighAngerThreshold;
            })
            .Select(a => a.Name)
            .ToArray();
        if (angryNearby.Length > 0)
        {
            return new { type = "AngryEncounterNearby", targets = angryNearby };
        }

        if (agent.Hunger >= SignificantSituationDetector.HighHungerAndSocialThreshold
            && agent.SocialNeed >= SignificantSituationDetector.HighSocialNeedThreshold)
        {
            return new
            {
                type = "GoalConflict",
                hunger = Round(agent.Hunger),
                socialNeed = Round(agent.SocialNeed),
            };
        }

        return new { type = "Routine" };
    }

    private static double Round(double v) => Math.Round(v, 3);
}