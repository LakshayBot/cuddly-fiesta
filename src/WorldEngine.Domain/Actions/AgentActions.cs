using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Simulation;

namespace WorldEngine.Domain.Actions;

public sealed class EatAction : IAgentAction
{
    public const double HungerReduction = 0.3;
    public const double FoodConsumed = 1.0;

    public string ActionType => ActionTypes.Eat;

    public bool IsAvailable(ActionContext context) =>
        context.GetInventoryQuantity(ResourceTypes.Food) >= FoodConsumed;

    public void Execute(ActionContext context)
    {
        var inv = context.EnsureInventory(ResourceTypes.Food);
        var before = inv.Quantity;
        inv.Quantity = Math.Max(0.0, inv.Quantity - FoodConsumed);
        inv.UpdatedAt = context.Now;

        var hungerBefore = context.Agent.Hunger;
        context.Agent.Hunger = Math.Clamp(context.Agent.Hunger - HungerReduction, 0.0, 1.0);
        context.Agent.Energy = Math.Max(0.0, context.Agent.Energy - ActionTuning.EatEnergyCost);
        context.Agent.UpdatedAt = context.Now;

        context.AppendEvent(SimulationEventTypes.AgentAte, context.Agent.Id, null, context.CurrentLocation.Id, new
        {
            foodConsumed = FoodConsumed,
            inventoryBefore = before,
            inventoryAfter = inv.Quantity,
            hungerBefore,
            hungerAfter = context.Agent.Hunger,
        });
    }
}

public sealed class RestAction : IAgentAction
{
    public const double EnergyRestored = 0.5;

    public string ActionType => ActionTypes.Rest;

    public bool IsAvailable(ActionContext context) => context.Agent.Alive;

    public void Execute(ActionContext context)
    {
        var energyBefore = context.Agent.Energy;
        context.Agent.Energy = Math.Clamp(context.Agent.Energy + EnergyRestored, 0.0, 1.0);
        context.Agent.UpdatedAt = context.Now;

        context.AppendEvent(SimulationEventTypes.AgentRested, context.Agent.Id, null, context.CurrentLocation.Id, new
        {
            energyBefore,
            energyAfter = context.Agent.Energy,
        });
    }
}

public sealed class MoveAction : IAgentAction
{
    public MoveAction(string targetLocationName)
    {
        TargetLocationName = targetLocationName;
    }

    public string TargetLocationName { get; }

    public string ActionType => ActionTypes.Move;

    public bool IsAvailable(ActionContext context) =>
        context.CurrentLocation.Name != TargetLocationName
        && context.LocationsByName.ContainsKey(TargetLocationName);

    public void Execute(ActionContext context)
    {
        if (!context.LocationsByName.TryGetValue(TargetLocationName, out var target))
        {
            return;
        }

        var fromName = context.CurrentLocation.Name;
        context.Agent.Location = target.Name;
        context.Agent.Energy = Math.Max(0.0, context.Agent.Energy - ActionTuning.MoveEnergyCost);
        context.Agent.UpdatedAt = context.Now;

        context.AppendEvent(SimulationEventTypes.AgentMoved, context.Agent.Id, null, target.Id, new
        {
            from = fromName,
            to = target.Name,
        });
    }
}

public sealed class HarvestFoodAction : IAgentAction
{
    public const double Amount = 1.0;

    public string ActionType => ActionTypes.HarvestFood;

    public bool IsAvailable(ActionContext context) =>
        context.CurrentLocation.Type == LocationTypes.Farm
        && context.GetLocationResourceQuantity(context.CurrentLocation.Id, ResourceTypes.Food) >= Amount
        && context.Agent.Energy >= ActionTuning.WorkEnergyCost;

    public void Execute(ActionContext context)
    {
        var farmStock = context.EnsureLocationResource(context.CurrentLocation.Id, ResourceTypes.Food);
        var before = farmStock.Quantity;
        farmStock.Quantity = Math.Max(0.0, farmStock.Quantity - Amount);
        farmStock.UpdatedAt = context.Now;

        var inv = context.EnsureInventory(ResourceTypes.Food);
        inv.Quantity += Amount;
        inv.UpdatedAt = context.Now;

        context.Agent.Energy = Math.Max(0.0, context.Agent.Energy - ActionTuning.WorkEnergyCost);
        context.Agent.UpdatedAt = context.Now;

        context.AppendEvent(SimulationEventTypes.AgentHarvestedFood, context.Agent.Id, null, context.CurrentLocation.Id, new
        {
            amount = Amount,
            farmStockBefore = before,
            farmStockAfter = farmStock.Quantity,
            inventoryAfter = inv.Quantity,
        });
    }
}

public sealed class GatherWoodAction : IAgentAction
{
    public const double Amount = 1.0;

    public const double FoodForaged = 0.5;

    public string ActionType => ActionTypes.GatherWood;

    public bool IsAvailable(ActionContext context) =>
        context.CurrentLocation.Type == LocationTypes.Forest
        && context.GetLocationResourceQuantity(context.CurrentLocation.Id, ResourceTypes.Wood) >= Amount
        && context.Agent.Energy >= ActionTuning.WorkEnergyCost;

    public void Execute(ActionContext context)
    {
        var forestStock = context.EnsureLocationResource(context.CurrentLocation.Id, ResourceTypes.Wood);
        var before = forestStock.Quantity;
        forestStock.Quantity = Math.Max(0.0, forestStock.Quantity - Amount);
        forestStock.UpdatedAt = context.Now;

        var woodInv = context.EnsureInventory(ResourceTypes.Wood);
        woodInv.Quantity += Amount;
        woodInv.UpdatedAt = context.Now;

        var foodInv = context.EnsureInventory(ResourceTypes.Food);
        foodInv.Quantity += FoodForaged;
        foodInv.UpdatedAt = context.Now;

        context.Agent.Energy = Math.Max(0.0, context.Agent.Energy - ActionTuning.WorkEnergyCost);
        context.Agent.UpdatedAt = context.Now;

        context.AppendEvent(SimulationEventTypes.AgentGatheredWood, context.Agent.Id, null, context.CurrentLocation.Id, new
        {
            amount = Amount,
            foodForaged = FoodForaged,
            forestStockBefore = before,
            forestStockAfter = forestStock.Quantity,
            woodInventoryAfter = woodInv.Quantity,
            foodInventoryAfter = foodInv.Quantity,
        });
    }
}

public sealed class WorkAction : IAgentAction
{
    public const decimal MoneyEarned = 2m;

    public const double FoodEarned = 1.0;

    public string ActionType => ActionTypes.Work;

    public bool IsAvailable(ActionContext context) =>
        context.CurrentLocation.Type == LocationTypes.Village
        && context.Agent.Energy >= ActionTuning.WorkEnergyCost;

    public void Execute(ActionContext context)
    {
        context.Agent.Money += MoneyEarned;

        var inv = context.EnsureInventory(ResourceTypes.Food);
        inv.Quantity += FoodEarned;
        inv.UpdatedAt = context.Now;

        context.Agent.Energy = Math.Max(0.0, context.Agent.Energy - ActionTuning.WorkEnergyCost);
        context.Agent.UpdatedAt = context.Now;

        context.AppendEvent(SimulationEventTypes.AgentWorked, context.Agent.Id, null, context.CurrentLocation.Id, new
        {
            moneyEarned = MoneyEarned,
            foodEarned = FoodEarned,
            moneyAfter = context.Agent.Money,
            inventoryAfter = inv.Quantity,
        });
    }
}

public sealed class IdleAction : IAgentAction
{
    public string ActionType => ActionTypes.Idle;

    public bool IsAvailable(ActionContext context) => true;

    public void Execute(ActionContext context)
    {
    }
}

public static class ActionTuning
{
    public const double WorkEnergyCost = 0.05;
    public const double MoveEnergyCost = 0.02;
    public const double EatEnergyCost = 0.0;
    public const double TradeEnergyCost = 0.02;
    public const double StealEnergyCost = 0.03;
}