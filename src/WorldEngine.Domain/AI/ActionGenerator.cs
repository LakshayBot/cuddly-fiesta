using WorldEngine.Domain;
using WorldEngine.Domain.Actions;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Simulation;

namespace WorldEngine.Domain.AI;

public sealed class ActionGenerator : IActionGenerator
{
    public IReadOnlyList<ActionProposal> Generate(AgentDecisionContext context)
    {
        var proposals = new List<ActionProposal>();

        proposals.Add(new ActionProposal(
            ActionId: ActionTypes.Eat,
            ActionType: ActionTypes.Eat,
            Action: new EatAction(),
            Description: "Eat available food to reduce hunger.",
            Metadata: new Dictionary<string, object>()));

        proposals.Add(new ActionProposal(
            ActionId: ActionTypes.Rest,
            ActionType: ActionTypes.Rest,
            Action: new RestAction(),
            Description: "Rest to recover energy.",
            Metadata: new Dictionary<string, object>()));

        foreach (var target in context.NearbyAgents)
        {
            if (!target.Alive || target.Id == context.Agent.Id) continue;

            proposals.Add(new ActionProposal(
                ActionId: $"{ActionTypes.Talk}:{target.Id}",
                ActionType: ActionTypes.Talk,
                Action: new TalkAction(target),
                Description: $"Talk with {target.Name} to build familiarity.",
                Metadata: new Dictionary<string, object> { ["targetId"] = target.Id }));

            if (context.GetInventoryQuantity(ResourceTypes.Food) >= HelpAction.FoodTransferred)
            {
                proposals.Add(new ActionProposal(
                    ActionId: $"{ActionTypes.Help}:{target.Id}",
                    ActionType: ActionTypes.Help,
                    Action: new HelpAction(target),
                    Description: $"Help {target.Name} by sharing food.",
                    Metadata: new Dictionary<string, object> { ["targetId"] = target.Id }));
            }

            if (context.Agent.Generosity >= 0.5
                && context.GetInventoryQuantity(ResourceTypes.Food) > ShareFoodAction.FoodTransferred + 1.0)
            {
                proposals.Add(new ActionProposal(
                    ActionId: $"{ActionTypes.ShareFood}:{target.Id}",
                    ActionType: ActionTypes.ShareFood,
                    Action: new ShareFoodAction(target),
                    Description: $"Give food to {target.Name} from excess.",
                    Metadata: new Dictionary<string, object> { ["targetId"] = target.Id }));
            }

            if (context.GetInventoryQuantity(ResourceTypes.Food) >= 2.0
                && target.Money >= TradeDefaults.FoodPrice)
            {
                proposals.Add(new ActionProposal(
                    ActionId: $"{ActionTypes.Trade}:{target.Id}",
                    ActionType: ActionTypes.Trade,
                    Action: new TradeAction(target, ResourceTypes.Food, TradeDefaults.FoodPrice),
                    Description: $"Sell food to {target.Name} for {TradeDefaults.FoodPrice:0.##} money.",
                    Metadata: new Dictionary<string, object> { ["targetId"] = target.Id }));
            }

            if (context.Agent.Hunger >= StealAction.HungerTrigger
                && context.GetOtherAgentInventoryQuantity(target.Id, ResourceTypes.Food) >= 1.0)
            {
                proposals.Add(new ActionProposal(
                    ActionId: $"{ActionTypes.Steal}:{target.Id}",
                    ActionType: ActionTypes.Steal,
                    Action: new StealAction(target),
                    Description: $"Steal food from {target.Name}.",
                    Metadata: new Dictionary<string, object> { ["targetId"] = target.Id }));
            }
        }

        switch (context.Agent.Occupation)
        {
            case Occupations.Farmer:
                proposals.Add(new ActionProposal(
                    ActionId: ActionTypes.HarvestFood,
                    ActionType: ActionTypes.HarvestFood,
                    Action: new HarvestFoodAction(),
                    Description: "Harvest food from the farm.",
                    Metadata: new Dictionary<string, object>()));
                if (context.LocationsByName.ContainsKey(LocationTypes.Farm)
                    && context.CurrentLocation.Type != LocationTypes.Farm)
                {
                    proposals.Add(new ActionProposal(
                        ActionId: $"{ActionTypes.Move}:{LocationTypes.Farm}",
                        ActionType: ActionTypes.Move,
                        Action: new MoveAction(LocationTypes.Farm),
                        Description: "Move to Farm to work.",
                        Metadata: new Dictionary<string, object> { ["target"] = LocationTypes.Farm }));
                }
                break;

            case Occupations.Woodcutter:
                proposals.Add(new ActionProposal(
                    ActionId: ActionTypes.GatherWood,
                    ActionType: ActionTypes.GatherWood,
                    Action: new GatherWoodAction(),
                    Description: "Gather wood from the forest.",
                    Metadata: new Dictionary<string, object>()));
                if (context.LocationsByName.ContainsKey(LocationTypes.Forest)
                    && context.CurrentLocation.Type != LocationTypes.Forest)
                {
                    proposals.Add(new ActionProposal(
                        ActionId: $"{ActionTypes.Move}:{LocationTypes.Forest}",
                        ActionType: ActionTypes.Move,
                        Action: new MoveAction(LocationTypes.Forest),
                        Description: "Move to Forest to work.",
                        Metadata: new Dictionary<string, object> { ["target"] = LocationTypes.Forest }));
                }
                break;

            case Occupations.Worker:
                proposals.Add(new ActionProposal(
                    ActionId: ActionTypes.Work,
                    ActionType: ActionTypes.Work,
                    Action: new WorkAction(),
                    Description: "Work at the village to earn money and food.",
                    Metadata: new Dictionary<string, object>()));
                if (context.LocationsByName.ContainsKey(LocationTypes.Village)
                    && context.CurrentLocation.Type != LocationTypes.Village)
                {
                    proposals.Add(new ActionProposal(
                        ActionId: $"{ActionTypes.Move}:{LocationTypes.Village}",
                        ActionType: ActionTypes.Move,
                        Action: new MoveAction(LocationTypes.Village),
                        Description: "Move to Village to work.",
                        Metadata: new Dictionary<string, object> { ["target"] = LocationTypes.Village }));
                }
                break;
        }

        proposals.Add(new ActionProposal(
            ActionId: ActionTypes.Idle,
            ActionType: ActionTypes.Idle,
            Action: new IdleAction(),
            Description: "Idle (no-op).",
            Metadata: new Dictionary<string, object>()));

        return proposals;
    }
}