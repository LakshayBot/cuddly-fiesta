using WorldEngine.Domain.Entities;

namespace WorldEngine.Domain.Actions;

public sealed class StealAction : IAgentAction
{
    public const double Quantity = 1.0;

    public const double HungerTrigger = 0.8;

    public StealAction(Agent target)
    {
        Target = target;
    }

    public Agent Target { get; }

    public string ActionType => ActionTypes.Steal;

    public bool IsAvailable(ActionContext context) =>
        context.Agent.Alive
        && Target.Alive
        && Target.Id != context.Agent.Id
        && Target.Location == context.Agent.Location
        && context.Agent.Hunger >= HungerTrigger
        && context.GetOtherAgentInventoryQuantity(Target.Id, ResourceTypes.Food) >= Quantity;

    public void Execute(ActionContext context)
    {
        var targetInv = context.EnsureInventoryFor(Target.Id, ResourceTypes.Food);
        targetInv.Quantity = Math.Max(0.0, targetInv.Quantity - Quantity);
        targetInv.UpdatedAt = context.Now;

        var actorInv = context.EnsureInventory(ResourceTypes.Food);
        actorInv.Quantity += Quantity;
        actorInv.UpdatedAt = context.Now;

        context.Agent.Energy = Math.Max(0.0, context.Agent.Energy - ActionTuning.StealEnergyCost);
        context.Agent.UpdatedAt = context.Now;
        Target.UpdatedAt = context.Now;

        context.AppendEvent(SimulationEventTypes.AgentStole, context.Agent.Id, Target.Id, context.CurrentLocation.Id, new
        {
            targetName = Target.Name,
            amount = Quantity,
            targetInventoryAfter = targetInv.Quantity,
            actorInventoryAfter = actorInv.Quantity,
        });
    }
}