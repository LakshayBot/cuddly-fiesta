using WorldEngine.Domain.Entities;

namespace WorldEngine.Domain.Actions;

public sealed class TradeAction : IAgentAction
{
    public const double Quantity = 1.0;

    public TradeAction(Agent target, string resourceType, decimal price)
    {
        Target = target;
        ResourceType = resourceType;
        Price = price;
    }

    public Agent Target { get; }

    public string ResourceType { get; }

    public decimal Price { get; }

    public string ActionType => ActionTypes.Trade;

    public bool IsAvailable(ActionContext context) =>
        context.Agent.Alive
        && Target.Alive
        && Target.Id != context.Agent.Id
        && Target.Location == context.Agent.Location
        && context.GetInventoryQuantity(ResourceType) >= Quantity
        && Target.Money >= Price;

    public void Execute(ActionContext context)
    {
        var actorInv = context.EnsureInventory(ResourceType);
        actorInv.Quantity = Math.Max(0.0, actorInv.Quantity - Quantity);
        actorInv.UpdatedAt = context.Now;

        var targetInv = context.EnsureInventoryFor(Target.Id, ResourceType);
        targetInv.Quantity += Quantity;
        targetInv.UpdatedAt = context.Now;

        context.Agent.Money += Price;
        Target.Money -= Price;

        context.Agent.Energy = Math.Max(0.0, context.Agent.Energy - ActionTuning.TradeEnergyCost);
        context.Agent.UpdatedAt = context.Now;
        Target.UpdatedAt = context.Now;

        context.AppendEvent(SimulationEventTypes.AgentTraded, context.Agent.Id, Target.Id, context.CurrentLocation.Id, new
        {
            targetName = Target.Name,
            resourceType = ResourceType,
            quantity = Quantity,
            pricePerUnit = Price,
            totalPrice = Price * (decimal)Quantity,
            actorMoneyAfter = context.Agent.Money,
            targetMoneyAfter = Target.Money,
            actorInventoryAfter = actorInv.Quantity,
            targetInventoryAfter = targetInv.Quantity,
        });
    }
}