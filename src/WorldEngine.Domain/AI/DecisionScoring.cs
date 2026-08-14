using WorldEngine.Domain.Actions;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Simulation;

namespace WorldEngine.Domain.AI;

public sealed class ScoreAccumulator
{
    public double Total { get; private set; }

    public List<DecisionFactor> Factors { get; } = new();

    public void Add(double contribution, FactorType type, string name, double value, string description, string? targetName = null)
    {
        if (contribution == 0)
        {
            return;
        }
        Total += contribution;
        Factors.Add(new DecisionFactor(type, name, targetName, value, contribution, description));
    }
}

public sealed record ScoredActionOutcome(double Score, IReadOnlyList<DecisionFactor> Factors);

public static class DecisionScoring
{
    public const double EatSurvivalScore = 95;

    public const double EatUrgentScore = 50;

    public const double EatBaselineScore = 10;

    public const double RestCriticalScore = 80;

    public const double RestLowScore = 50;

    public const double RestBaselineScore = 5;

    public const double WorkBaselineScore = 35;

    public const double MoveToWorkScore = 30;

    public const double TalkBaselineScore = 25;

    public const double HelpBaselineScore = 60;

    public const double ShareFoodBaselineScore = 45;

    public const double TradeBaselineScore = 55;

    public const double StealBaselineScore = 70;

    public const double ProductionBaselineScore = 50;

    public const double PersonalityBonusMultiplier = 15;

    public static ScoredActionOutcome Score(ActionProposal proposal, AgentDecisionContext context)
    {
        var acc = new ScoreAccumulator();
        switch (proposal.ActionType)
        {
            case ActionTypes.Eat: ScoreEat(acc, context); break;
            case ActionTypes.Rest: ScoreRest(acc, context); break;
            case ActionTypes.HarvestFood: ScoreProduction(acc, context, Occupations.Farmer, "Harvest food at the farm"); break;
            case ActionTypes.GatherWood: ScoreProduction(acc, context, Occupations.Woodcutter, "Gather wood at the forest"); break;
            case ActionTypes.Work: ScoreWork(acc, context); break;
            case ActionTypes.Move: ScoreMove(acc, proposal, context); break;
            case ActionTypes.Talk: ScoreTalk(acc, proposal, context); break;
            case ActionTypes.Help: ScoreHelp(acc, proposal, context); break;
            case ActionTypes.ShareFood: ScoreShare(acc, proposal, context); break;
            case ActionTypes.Trade: ScoreTrade(acc, proposal, context); break;
            case ActionTypes.Steal: ScoreSteal(acc, proposal, context); break;
            case ActionTypes.Idle: acc.Add(0, FactorType.Baseline, "Idle", 0, "Do nothing."); break;
        }
        return new ScoredActionOutcome(acc.Total, acc.Factors);
    }

    public static double BaseScore(ActionProposal proposal, AgentDecisionContext context) =>
        Score(proposal, context).Score;

    public static double PersonalityModifier(ActionProposal proposal, AgentDecisionContext context)
    {
        var agent = context.Agent;
        return proposal.ActionType switch
        {
            ActionTypes.Help => (agent.Generosity - 0.5) * PersonalityBonusMultiplier,
            ActionTypes.ShareFood => (agent.Generosity - 0.5) * PersonalityBonusMultiplier,
            ActionTypes.Work => (agent.Discipline - 0.5) * PersonalityBonusMultiplier + (agent.Ambition - 0.5) * PersonalityBonusMultiplier,
            ActionTypes.HarvestFood => (agent.Discipline - 0.5) * PersonalityBonusMultiplier,
            ActionTypes.GatherWood => (agent.Discipline - 0.5) * PersonalityBonusMultiplier,
            ActionTypes.Talk => (agent.Sociability - 0.5) * PersonalityBonusMultiplier + (agent.Empathy - 0.5) * PersonalityBonusMultiplier,
            _ => 0,
        };
    }

    private static void ScoreEat(ScoreAccumulator acc, AgentDecisionContext context)
    {
        var hunger = context.Agent.Hunger;
        if (hunger >= NeedRates.HungerPanicThreshold)
        {
            acc.Add(EatSurvivalScore, FactorType.Need, "Hunger", hunger,
                "Severe hunger strongly increased the priority of obtaining food.");
            acc.Add(hunger * 10, FactorType.Need, "HungerPressure", hunger,
                "Extreme hunger added urgency to eating.");
        }
        else if (hunger >= NeedRates.HungerUrgentThreshold)
        {
            acc.Add(EatUrgentScore, FactorType.Need, "Hunger", hunger,
                "Hunger is high; food became a priority.");
        }
        else
        {
            acc.Add(EatBaselineScore, FactorType.Baseline, "EatBaseline", hunger,
                "Low baseline urge to eat when hunger is mild.");
        }
    }

    private static void ScoreRest(ScoreAccumulator acc, AgentDecisionContext context)
    {
        var energy = context.Agent.Energy;
        if (energy <= NeedRates.EnergyRestThreshold)
        {
            acc.Add(RestCriticalScore, FactorType.Need, "Energy", energy,
                "Exhaustion made rest critical.");
        }
        else if (energy <= 0.30)
        {
            acc.Add(RestLowScore, FactorType.Need, "Energy", energy,
                "Low energy made resting worthwhile.");
        }
        else
        {
            acc.Add(RestBaselineScore, FactorType.Baseline, "RestBaseline", energy,
                "Low baseline preference for rest while energy is adequate.");
        }
    }

    private static void ScoreProduction(ScoreAccumulator acc, AgentDecisionContext context, string occupation, string what)
    {
        if (context.Agent.Occupation != occupation)
        {
            return;
        }
        acc.Add(ProductionBaselineScore, FactorType.Opportunity, "Occupation", 1,
            $"Occupation ({occupation}) supports {what}.");
        acc.Add((context.Agent.Discipline - 0.5) * PersonalityBonusMultiplier, FactorType.Personality, "Discipline", context.Agent.Discipline,
            "Discipline increased the appeal of productive work.");
    }

    private static void ScoreWork(ScoreAccumulator acc, AgentDecisionContext context)
    {
        if (context.Agent.Occupation != Occupations.Worker)
        {
            return;
        }
        acc.Add(WorkBaselineScore, FactorType.Opportunity, "Occupation", 1,
            "Occupation (Worker) enables paid work.");
        acc.Add((context.Agent.Discipline - 0.5) * PersonalityBonusMultiplier, FactorType.Personality, "Discipline", context.Agent.Discipline,
            "Discipline increased the appeal of working.");
        acc.Add((context.Agent.Ambition - 0.5) * PersonalityBonusMultiplier, FactorType.Personality, "Ambition", context.Agent.Ambition,
            "Ambition increased the appeal of earning money.");
    }

    private static void ScoreMove(ScoreAccumulator acc, ActionProposal proposal, AgentDecisionContext context)
    {
        if (proposal.Action is not MoveAction move)
        {
            return;
        }

        if (context.CurrentLocation.Type == move.TargetLocationName)
        {
            return;
        }

        var matchesOccupation = move.TargetLocationName switch
        {
            LocationTypes.Farm => context.Agent.Occupation == Occupations.Farmer,
            LocationTypes.Forest => context.Agent.Occupation == Occupations.Woodcutter,
            LocationTypes.Village => context.Agent.Occupation == Occupations.Worker,
            _ => false,
        };

        var score = matchesOccupation ? MoveToWorkScore : 5;
        acc.Add(score, FactorType.Opportunity, "Move", 1,
            matchesOccupation
                ? $"Moving to {move.TargetLocationName} enables productive work for this occupation."
                : $"General urge to move to {move.TargetLocationName}.");
    }

    private static void ScoreTalk(ScoreAccumulator acc, ActionProposal proposal, AgentDecisionContext context)
    {
        if (proposal.Action is not TalkAction talk || !talk.Target.Alive)
        {
            return;
        }

        acc.Add(TalkBaselineScore, FactorType.Baseline, "TalkBaseline", 1,
            "Baseline social urge to talk.");

        acc.Add(context.Agent.SocialNeed * 30, FactorType.Need, "SocialNeed", context.Agent.SocialNeed,
            "Loneliness increased the urge to talk.");

        var rel = context.FindRelationshipWith(talk.Target.Id);
        if (rel is not null)
        {
            acc.Add(rel.Trust * 5, FactorType.Relationship, "Trust", rel.Trust,
                "Existing trust made conversation more appealing.", talk.Target.Name);
            acc.Add(rel.Affection * 5, FactorType.Relationship, "Affection", rel.Affection,
                "Affection made conversation more appealing.", talk.Target.Name);
            if (rel.Anger > 0.5)
            {
                acc.Add(-20, FactorType.Relationship, "Anger", rel.Anger,
                    "Anger made conversation with this agent undesirable.", talk.Target.Name);
            }
        }
    }

    private static void ScoreHelp(ScoreAccumulator acc, ActionProposal proposal, AgentDecisionContext context)
    {
        if (proposal.Action is not HelpAction help || !help.Target.Alive)
        {
            return;
        }

        if (help.Target.Hunger < 0.3)
        {
            return;
        }

        acc.Add(HelpBaselineScore, FactorType.Baseline, "HelpBaseline", 1,
            "Baseline willingness to help.");

        acc.Add(help.Target.Hunger * 30, FactorType.Need, "TargetHunger", help.Target.Hunger,
            "The target's hunger strongly motivated helping.", help.Target.Name);

        acc.Add((context.Agent.Generosity - 0.5) * PersonalityBonusMultiplier, FactorType.Personality, "Generosity", context.Agent.Generosity,
            "Generosity increased the appeal of helping.");

        var rel = context.FindRelationshipWith(help.Target.Id);
        if (rel is not null)
        {
            acc.Add(rel.Trust * 10, FactorType.Relationship, "Trust", rel.Trust,
                "A strong trusting relationship increased the desire to help.", help.Target.Name);
            acc.Add(rel.Affection * 8, FactorType.Relationship, "Affection", rel.Affection,
                "Affection increased the desire to help.", help.Target.Name);
        }

        AddMemoryInfluence(acc, context, help.Target.Id, "help", 12);
    }

    private static void AddMemoryInfluence(
        ScoreAccumulator acc,
        AgentDecisionContext context,
        Guid targetId,
        string verb,
        double weight)
    {
        var memory = context.RecentMemories
            .Where(m => m.OtherAgentId == targetId
                && (m.Type == MemoryTypes.ReceivedHelp || m.Type == MemoryTypes.ReceivedFood || m.Type == MemoryTypes.GaveFood))
            .OrderByDescending(m => m.Importance)
            .FirstOrDefault();

        if (memory is null)
        {
            return;
        }

        acc.Add(memory.Importance * weight, FactorType.Memory, "Memory", memory.Importance,
            $"A past memory of this person ({memory.Summary}) increased the desire to {verb} them.",
            context.NearbyAgents.FirstOrDefault(a => a.Id == targetId)?.Name);
    }

    private static void ScoreShare(ScoreAccumulator acc, ActionProposal proposal, AgentDecisionContext context)
    {
        if (proposal.Action is not ShareFoodAction share || !share.Target.Alive)
        {
            return;
        }

        acc.Add(ShareFoodBaselineScore, FactorType.Baseline, "ShareBaseline", 1,
            "Baseline willingness to share surplus food.");

        acc.Add(share.Target.Hunger * 20, FactorType.Need, "TargetHunger", share.Target.Hunger,
            "The target's hunger increased the value of sharing.", share.Target.Name);

        acc.Add((context.Agent.Generosity - 0.5) * PersonalityBonusMultiplier, FactorType.Personality, "Generosity", context.Agent.Generosity,
            "Generosity increased the appeal of sharing food.");

        var rel = context.FindRelationshipWith(share.Target.Id);
        if (rel is not null)
        {
            acc.Add(rel.Trust * 8, FactorType.Relationship, "Trust", rel.Trust,
                "Trust increased the appeal of sharing.", share.Target.Name);
            acc.Add(rel.Affection * 8, FactorType.Relationship, "Affection", rel.Affection,
                "Affection increased the appeal of sharing.", share.Target.Name);
        }
    }

    private static void ScoreTrade(ScoreAccumulator acc, ActionProposal proposal, AgentDecisionContext context)
    {
        if (proposal.Action is not TradeAction trade || !trade.Target.Alive)
        {
            return;
        }

        if (context.GetInventoryQuantity(trade.ResourceType) < TradeAction.Quantity + 1.0)
        {
            return;
        }
        if (trade.Target.Money < trade.Price)
        {
            return;
        }

        acc.Add(TradeBaselineScore, FactorType.Baseline, "TradeBaseline", 1,
            "Baseline willingness to trade.");

        acc.Add((context.Agent.Ambition - 0.5) * 20, FactorType.Personality, "Ambition", context.Agent.Ambition,
            "Ambition increased the appeal of trading for money.");

        acc.Add(trade.Target.Hunger * 10, FactorType.Need, "TargetHunger", trade.Target.Hunger,
            "The target's hunger made the trade more valuable to them.", trade.Target.Name);

        var rel = context.FindRelationshipWith(trade.Target.Id);
        if (rel is not null)
        {
            acc.Add(rel.Trust * 5, FactorType.Relationship, "Trust", rel.Trust,
                "Trust made the trade more comfortable.", trade.Target.Name);
            if (rel.Anger > 0.5)
            {
                acc.Add(-15, FactorType.Relationship, "Anger", rel.Anger,
                    "Anger made trading with this agent unappealing.", trade.Target.Name);
            }
        }
    }

    private static void ScoreSteal(ScoreAccumulator acc, ActionProposal proposal, AgentDecisionContext context)
    {
        if (proposal.Action is not StealAction steal || !steal.Target.Alive)
        {
            return;
        }

        if (context.Agent.Hunger < StealAction.HungerTrigger)
        {
            return;
        }
        if (context.GetOtherAgentInventoryQuantity(steal.Target.Id, ResourceTypes.Food) < 1.0)
        {
            return;
        }

        acc.Add(StealBaselineScore, FactorType.Baseline, "StealBaseline", 1,
            "Baseline willingness to steal when desperate.");

        acc.Add((context.Agent.Hunger - 0.8) * 100, FactorType.Need, "Hunger", context.Agent.Hunger,
            "Severe hunger strongly increased the urge to steal.");

        acc.Add((context.Agent.Aggression - 0.5) * 40, FactorType.Personality, "Aggression", context.Agent.Aggression,
            "Aggression increased the willingness to steal.");

        var rel = context.FindRelationshipWith(steal.Target.Id);
        if (rel is not null)
        {
            acc.Add(rel.Anger * 20, FactorType.Relationship, "Anger", rel.Anger,
                "Anger toward the target made stealing easier to justify.", steal.Target.Name);
            if (rel.Trust >= 0.7)
            {
                acc.Add(-30, FactorType.Relationship, "Trust", rel.Trust,
                    "A trusting relationship made stealing from this agent hard to justify.", steal.Target.Name);
            }
        }
    }
}