using WorldEngine.Domain.Actions;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Simulation;

namespace WorldEngine.Domain.AI;

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

    public static double BaseScore(ActionProposal proposal, AgentDecisionContext context)
    {
        var agent = context.Agent;
        return proposal.ActionType switch
        {
            ActionTypes.Eat => ScoreEat(agent),
            ActionTypes.Rest => ScoreRest(agent),
            ActionTypes.HarvestFood => agent.Occupation == Occupations.Farmer ? ProductionBaselineScore : 0,
            ActionTypes.GatherWood => agent.Occupation == Occupations.Woodcutter ? ProductionBaselineScore : 0,
            ActionTypes.Work => agent.Occupation == Occupations.Worker ? WorkBaselineScore : 0,
            ActionTypes.Move => ScoreMove(proposal, context),
            ActionTypes.Talk => ScoreTalk(proposal, context),
            ActionTypes.Help => ScoreHelp(proposal, context),
            ActionTypes.ShareFood => ScoreShare(proposal, context),
            ActionTypes.Trade => ScoreTrade(proposal, context),
            ActionTypes.Steal => ScoreSteal(proposal, context),
            ActionTypes.Idle => 0,
            _ => 0,
        };
    }

    private static double ScoreEat(Agent agent)
    {
        if (agent.Hunger >= NeedRates.HungerPanicThreshold) return EatSurvivalScore;
        if (agent.Hunger >= NeedRates.HungerUrgentThreshold) return EatUrgentScore;
        return EatBaselineScore;
    }

    private static double ScoreRest(Agent agent)
    {
        if (agent.Energy <= NeedRates.EnergyRestThreshold) return RestCriticalScore;
        if (agent.Energy <= 0.30) return RestLowScore;
        return RestBaselineScore;
    }

    private static double ScoreMove(ActionProposal proposal, AgentDecisionContext context)
    {
        if (proposal.Action is not MoveAction move) return 0;

        if (context.CurrentLocation.Type == move.TargetLocationName) return 0;

        return move.TargetLocationName switch
        {
            LocationTypes.Farm => context.Agent.Occupation == Occupations.Farmer ? MoveToWorkScore : 5,
            LocationTypes.Forest => context.Agent.Occupation == Occupations.Woodcutter ? MoveToWorkScore : 5,
            LocationTypes.Village => context.Agent.Occupation == Occupations.Worker ? MoveToWorkScore : 5,
            _ => 5,
        };
    }

    private static double ScoreTalk(ActionProposal proposal, AgentDecisionContext context)
    {
        if (proposal.Action is not TalkAction talk) return 0;
        var target = talk.Target;
        if (!target.Alive) return 0;

        var score = TalkBaselineScore;
        score += context.Agent.SocialNeed * 30;

        var rel = context.FindRelationshipWith(target.Id);
        if (rel is not null)
        {
            score += rel.Trust * 5;
            score += rel.Affection * 5;
            if (rel.Anger > 0.5) score -= 20;
        }

        return score;
    }

    private static double ScoreHelp(ActionProposal proposal, AgentDecisionContext context)
    {
        if (proposal.Action is not HelpAction help) return 0;
        var target = help.Target;
        if (!target.Alive) return 0;

        if (target.Hunger < 0.3) return 0;

        var score = HelpBaselineScore;
        score += target.Hunger * 30;

        var rel = context.FindRelationshipWith(target.Id);
        if (rel is not null)
        {
            score += rel.Trust * 10;
            score += rel.Affection * 8;
        }

        return score;
    }

    private static double ScoreShare(ActionProposal proposal, AgentDecisionContext context)
    {
        if (proposal.Action is not ShareFoodAction share) return 0;
        var target = share.Target;
        if (!target.Alive) return 0;

        var score = ShareFoodBaselineScore;
        score += target.Hunger * 20;
        score += (context.Agent.Generosity - 0.5) * 20;

        var rel = context.FindRelationshipWith(target.Id);
        if (rel is not null)
        {
            score += rel.Trust * 8;
            score += rel.Affection * 8;
        }

        return score;
    }

    private static double ScoreTrade(ActionProposal proposal, AgentDecisionContext context)
    {
        if (proposal.Action is not TradeAction trade) return 0;
        var target = trade.Target;
        if (!target.Alive) return 0;

        if (context.GetInventoryQuantity(trade.ResourceType) < TradeAction.Quantity + 1.0) return 0;
        if (target.Money < trade.Price) return 0;

        var score = TradeBaselineScore;
        score += (context.Agent.Ambition - 0.5) * 20;
        score += target.Hunger * 10;

        var rel = context.FindRelationshipWith(target.Id);
        if (rel is not null)
        {
            score += rel.Trust * 5;
            if (rel.Anger > 0.5) score -= 15;
        }

        return score;
    }

    private static double ScoreSteal(ActionProposal proposal, AgentDecisionContext context)
    {
        if (proposal.Action is not StealAction steal) return 0;
        var target = steal.Target;
        if (!target.Alive) return 0;

        if (context.Agent.Hunger < StealAction.HungerTrigger) return 0;
        if (context.GetOtherAgentInventoryQuantity(target.Id, ResourceTypes.Food) < 1.0) return 0;

        var score = StealBaselineScore;
        score += (context.Agent.Hunger - 0.8) * 100;
        score += (context.Agent.Aggression - 0.5) * 40;

        var rel = context.FindRelationshipWith(target.Id);
        if (rel is not null)
        {
            score += rel.Anger * 20;
            if (rel.Trust >= 0.7) score -= 30;
        }

        return score;
    }

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
}