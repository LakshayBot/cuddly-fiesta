using WorldEngine.Domain.Entities;

namespace WorldEngine.Domain.Actions;

public sealed class TalkAction : IAgentAction
{
    public const double EnergyCost = 0.02;

    public TalkAction(Agent target)
    {
        Target = target;
    }

    public Agent Target { get; }

    public string ActionType => ActionTypes.Talk;

    public bool IsAvailable(ActionContext context) =>
        context.Agent.Alive
        && Target.Alive
        && Target.Id != context.Agent.Id
        && Target.Location == context.Agent.Location;

    public void Execute(ActionContext context)
    {
        context.Agent.Energy = Math.Max(0.0, context.Agent.Energy - EnergyCost);
        context.Agent.SocialNeed = Math.Clamp(context.Agent.SocialNeed - 0.05, 0.0, 1.0);
        context.Agent.UpdatedAt = context.Now;

        context.AppendEvent(SimulationEventTypes.AgentTalked, context.Agent.Id, Target.Id, context.CurrentLocation.Id, new
        {
            targetName = Target.Name,
        });
    }
}

public sealed class HelpAction : IAgentAction
{
    public const double EnergyCost = 0.03;

    public const double FoodTransferred = 1.0;

    public HelpAction(Agent target)
    {
        Target = target;
    }

    public Agent Target { get; }

    public string ActionType => ActionTypes.Help;

    public bool IsAvailable(ActionContext context) =>
        context.Agent.Alive
        && Target.Alive
        && Target.Id != context.Agent.Id
        && Target.Location == context.Agent.Location
        && context.GetInventoryQuantity(ResourceTypes.Food) >= FoodTransferred
        && context.Agent.Energy >= EnergyCost;

    public void Execute(ActionContext context)
    {
        var actorInv = context.EnsureInventory(ResourceTypes.Food);
        actorInv.Quantity = Math.Max(0.0, actorInv.Quantity - FoodTransferred);
        actorInv.UpdatedAt = context.Now;

        var targetInv = context.EnsureInventoryFor(Target.Id, ResourceTypes.Food);
        targetInv.Quantity += FoodTransferred;
        targetInv.UpdatedAt = context.Now;

        context.Agent.Energy = Math.Max(0.0, context.Agent.Energy - EnergyCost);
        context.Agent.UpdatedAt = context.Now;

        context.AppendEvent(SimulationEventTypes.AgentHelped, context.Agent.Id, Target.Id, context.CurrentLocation.Id, new
        {
            targetName = Target.Name,
            foodTransferred = FoodTransferred,
            actorInventoryAfter = actorInv.Quantity,
            targetInventoryAfter = targetInv.Quantity,
        });
    }
}

public sealed class ShareFoodAction : IAgentAction
{
    public const double EnergyCost = 0.02;

    public const double FoodTransferred = 1.0;

    public ShareFoodAction(Agent target)
    {
        Target = target;
    }

    public Agent Target { get; }

    public string ActionType => ActionTypes.ShareFood;

    public bool IsAvailable(ActionContext context) =>
        context.Agent.Alive
        && Target.Alive
        && Target.Id != context.Agent.Id
        && Target.Location == context.Agent.Location
        && context.GetInventoryQuantity(ResourceTypes.Food) >= FoodTransferred + 1.0
        && context.Agent.Energy >= EnergyCost;

    public void Execute(ActionContext context)
    {
        var actorInv = context.EnsureInventory(ResourceTypes.Food);
        actorInv.Quantity = Math.Max(0.0, actorInv.Quantity - FoodTransferred);
        actorInv.UpdatedAt = context.Now;

        var targetInv = context.EnsureInventoryFor(Target.Id, ResourceTypes.Food);
        targetInv.Quantity += FoodTransferred;
        targetInv.UpdatedAt = context.Now;

        context.Agent.Energy = Math.Max(0.0, context.Agent.Energy - EnergyCost);
        context.Agent.UpdatedAt = context.Now;

        context.AppendEvent(SimulationEventTypes.AgentSharedFood, context.Agent.Id, Target.Id, context.CurrentLocation.Id, new
        {
            targetName = Target.Name,
            foodTransferred = FoodTransferred,
            actorInventoryAfter = actorInv.Quantity,
            targetInventoryAfter = targetInv.Quantity,
        });
    }
}