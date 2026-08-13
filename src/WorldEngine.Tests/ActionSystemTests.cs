using Microsoft.EntityFrameworkCore;
using WorldEngine.Domain;
using WorldEngine.Domain.Actions;
using WorldEngine.Domain.AI;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Tests;

public class ActionSystemTests
{
    [Fact]
    public async Task Eat_AvailableWhenFoodInInventory()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village, food: 2.0);

        var ctx = await BuildContextAsync(harness, world, agent);

        var eat = new EatAction();
        Assert.True(eat.IsAvailable(ctx));
    }

    [Fact]
    public async Task Eat_NotAvailableWhenNoFood()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village, food: 0.0);

        var ctx = await BuildContextAsync(harness, world, agent);

        var eat = new EatAction();
        Assert.False(eat.IsAvailable(ctx));
    }

    [Fact]
    public async Task Eat_ReducesHunger()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village,
            food: 3.0, hunger: 0.9);

        var ctx = await BuildContextAsync(harness, world, agent);
        var hungerBefore = ctx.Agent.Hunger;
        var inventoryBefore = ctx.GetInventoryQuantity(ResourceTypes.Food);

        new EatAction().Execute(ctx);

        Assert.Equal(inventoryBefore - EatAction.FoodConsumed, ctx.GetInventoryQuantity(ResourceTypes.Food), 6);
        Assert.True(ctx.Agent.Hunger < hungerBefore);
        Assert.Equal(hungerBefore - EatAction.HungerReduction, ctx.Agent.Hunger, 6);
        Assert.Single(ctx.NewEvents);
        Assert.Equal(SimulationEventTypes.AgentAte, ctx.NewEvents[0].EventType);
    }

    [Fact]
    public async Task HarvestFood_AvailableAtFarmWithStock()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var farmLocation = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farmLocation.Id, ResourceTypes.Food, 10.0);

        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Farm);

        var ctx = await BuildContextAsync(harness, world, agent);
        Assert.Equal(LocationTypes.Farm, ctx.Agent.Location);
        Assert.Equal(LocationTypes.Farm, ctx.CurrentLocation.Name);
        Assert.Equal(10.0, ctx.GetLocationResourceQuantity(farmLocation.Id, ResourceTypes.Food), 6);
        Assert.True(ctx.Agent.Energy >= ActionTuning.WorkEnergyCost);
        Assert.True(new HarvestFoodAction().IsAvailable(ctx));
    }

    [Fact]
    public async Task HarvestFood_NotAvailableAtWrongLocation()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var farmLocation = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farmLocation.Id, ResourceTypes.Food, 10.0);

        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village);

        var ctx = await BuildContextAsync(harness, world, agent);
        Assert.False(new HarvestFoodAction().IsAvailable(ctx));
    }

    [Fact]
    public async Task HarvestFood_NotAvailableWhenFarmEmpty()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var farmLocation = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farmLocation.Id, ResourceTypes.Food, 0.0);

        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Farm, food: 0.0);

        var ctx = await BuildContextAsync(harness, world, agent);
        Assert.False(new HarvestFoodAction().IsAvailable(ctx));
    }

    [Fact]
    public async Task HarvestFood_TransfersStockToInventory()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var farmLocation = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farmLocation.Id, ResourceTypes.Food, 5.0);

        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Farm);

        var ctx = await BuildContextAsync(harness, world, agent);
        new HarvestFoodAction().Execute(ctx);

        Assert.Equal(5.0 - HarvestFoodAction.Amount, ctx.GetLocationResourceQuantity(farmLocation.Id, ResourceTypes.Food), 6);
        Assert.Equal(HarvestFoodAction.Amount, ctx.GetInventoryQuantity(ResourceTypes.Food), 6);
        Assert.Single(ctx.NewEvents);
        Assert.Equal(SimulationEventTypes.AgentHarvestedFood, ctx.NewEvents[0].EventType);
    }

    [Fact]
    public async Task Resources_NeverGoNegative_AfterManyHarvests()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var farmLocation = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farmLocation.Id, ResourceTypes.Food, 2.0);

        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Farm);

        for (var i = 0; i < 10; i++)
        {
            var ctx = await BuildContextAsync(harness, world, agent);
            var action = new HarvestFoodAction();
            if (action.IsAvailable(ctx))
            {
                action.Execute(ctx);
                await SaveContextAsync(harness.DbContextFactory, ctx);
            }
        }

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var resource = await db.LocationResources
            .AsNoTracking()
            .FirstAsync(lr => lr.LocationId == farmLocation.Id && lr.ResourceType == ResourceTypes.Food);
        Assert.True(resource.Quantity >= 0.0, $"Farm stock should not go negative, was {resource.Quantity}");

        var inv = await db.AgentInventories
            .AsNoTracking()
            .FirstOrDefaultAsync(ai => ai.AgentId == agent.Id && ai.ResourceType == ResourceTypes.Food);
        Assert.NotNull(inv);
        Assert.True(inv!.Quantity >= 0.0);
    }

    [Fact]
    public async Task Rest_RestoresEnergy()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village, energy: 0.2);

        var ctx = await BuildContextAsync(harness, world, agent);
        var energyBefore = ctx.Agent.Energy;

        new RestAction().Execute(ctx);

        Assert.Equal(energyBefore + RestAction.EnergyRestored, ctx.Agent.Energy, 6);
        Assert.Single(ctx.NewEvents);
        Assert.Equal(SimulationEventTypes.AgentRested, ctx.NewEvents[0].EventType);
    }

    [Fact]
    public async Task Work_EarnsMoney()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village, money: 5m);

        var ctx = await BuildContextAsync(harness, world, agent);
        new WorkAction().Execute(ctx);

        Assert.Equal(5m + WorkAction.MoneyEarned, ctx.Agent.Money);
        Assert.Single(ctx.NewEvents);
        Assert.Equal(SimulationEventTypes.AgentWorked, ctx.NewEvents[0].EventType);
    }

    [Fact]
    public async Task Harvest_NotAvailableWhenEnergyTooLow()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var farmLocation = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farmLocation.Id, ResourceTypes.Food, 10.0);

        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Farm, energy: 0.0);

        var ctx = await BuildContextAsync(harness, world, agent);
        Assert.False(new HarvestFoodAction().IsAvailable(ctx));
    }

    [Fact]
    public async Task Move_ChangesAgentLocation()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village);

        var ctx = await BuildContextAsync(harness, world, agent);
        new MoveAction(LocationTypes.Farm).Execute(ctx);

        Assert.Equal(LocationTypes.Farm, ctx.Agent.Location);
        Assert.Single(ctx.NewEvents);
        Assert.Equal(SimulationEventTypes.AgentMoved, ctx.NewEvents[0].EventType);
    }

    [Fact]
    public async Task DecisionEngine_EatsWhenHungryAndHasFood()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village,
            food: 5.0, hunger: 0.95);

        var actionCtx = await BuildContextAsync(harness, world, agent);

        var sim = actionCtx.Simulation;
        var decisionCtx = new AgentDecisionContext(
            agent: actionCtx.Agent,
            world: actionCtx.World,
            simulation: sim,
            currentLocation: actionCtx.CurrentLocation,
            locationsById: actionCtx.LocationsById,
            locationsByName: actionCtx.LocationsByName,
            locationResources: actionCtx.LocationResources,
            agentInventories: actionCtx.AgentInventories,
            nearbyAgents: actionCtx.NearbyAgents,
            outgoingRelationships: new List<AgentRelationship>(),
            recentMemories: new List<AgentMemory>(),
            actionContext: actionCtx,
            now: actionCtx.Now);

        var engine = new RuleBasedDecisionEngine(new ActionGenerator());
        var decision = await engine.DecideAsync(decisionCtx, CancellationToken.None);
        Assert.NotNull(decision.SelectedAction);
        Assert.IsType<EatAction>(decision.SelectedAction!.Action);
    }

    [Fact]
    public async Task DecisionEngine_RestsWhenExhaustedAndNotHungry()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Farm,
            energy: 0.05, hunger: 0.3);

        var actionCtx = await BuildContextAsync(harness, world, agent);

        var sim = actionCtx.Simulation;
        var decisionCtx = new AgentDecisionContext(
            agent: actionCtx.Agent,
            world: actionCtx.World,
            simulation: sim,
            currentLocation: actionCtx.CurrentLocation,
            locationsById: actionCtx.LocationsById,
            locationsByName: actionCtx.LocationsByName,
            locationResources: actionCtx.LocationResources,
            agentInventories: actionCtx.AgentInventories,
            nearbyAgents: actionCtx.NearbyAgents,
            outgoingRelationships: new List<AgentRelationship>(),
            recentMemories: new List<AgentMemory>(),
            actionContext: actionCtx,
            now: actionCtx.Now);

        var engine = new RuleBasedDecisionEngine(new ActionGenerator());
        var decision = await engine.DecideAsync(decisionCtx, CancellationToken.None);
        Assert.NotNull(decision.SelectedAction);
        Assert.IsType<RestAction>(decision.SelectedAction!.Action);
    }

    [Fact]
    public async Task DecisionEngine_FarmerAtFarmHarvests()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var farmLocation = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farmLocation.Id, ResourceTypes.Food, 10.0);

        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Farm, energy: 1.0, hunger: 0.2);

        var actionCtx = await BuildContextAsync(harness, world, agent);

        var sim = actionCtx.Simulation;
        var decisionCtx = new AgentDecisionContext(
            agent: actionCtx.Agent,
            world: actionCtx.World,
            simulation: sim,
            currentLocation: actionCtx.CurrentLocation,
            locationsById: actionCtx.LocationsById,
            locationsByName: actionCtx.LocationsByName,
            locationResources: actionCtx.LocationResources,
            agentInventories: actionCtx.AgentInventories,
            nearbyAgents: actionCtx.NearbyAgents,
            outgoingRelationships: new List<AgentRelationship>(),
            recentMemories: new List<AgentMemory>(),
            actionContext: actionCtx,
            now: actionCtx.Now);

        var engine = new RuleBasedDecisionEngine(new ActionGenerator());
        var decision = await engine.DecideAsync(decisionCtx, CancellationToken.None);
        Assert.NotNull(decision.SelectedAction);
        Assert.IsType<HarvestFoodAction>(decision.SelectedAction!.Action);
    }

    [Fact]
    public async Task Starvation_Death_WhenNoFoodAvailable()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldWithLocationsAsync(harness.DbContextFactory);
        var farmLocation = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farmLocation.Id, ResourceTypes.Food, 0.0);

        var agent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Unemployed, LocationTypes.Village,
            food: 0.0, hunger: 0.95, health: 0.05);

        for (var i = 0; i < 30; i++)
        {
            await harness.AgentSystem.ProcessAsync(
                new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
                    world.CurrentSimulationTime.AddSeconds(i * 60), world.CurrentSimulationTime.AddSeconds((i + 1) * 60), i + 1),
                CancellationToken.None);

            var fresh = await TestSetup.LoadAgentAsync(harness.DbContextFactory, agent.Id);
            if (fresh is { Alive: false })
            {
                break;
            }
        }

        var reloaded = await TestSetup.LoadAgentAsync(harness.DbContextFactory, agent.Id);
        Assert.False(reloaded!.Alive);
        Assert.Equal("Starvation", reloaded.DeathCause);
    }

    private static async Task<World> SeedWorldWithLocationsAsync(IDbContextFactory<WorldEngineDbContext> factory)
    {
        return await TestSetup.SeedWorldAsync(factory, SimulationStatus.Running, speed: 1.0);
    }

    private static async Task<Location> GetLocationAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        Guid worldId,
        string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Locations.AsNoTracking().FirstAsync(l => l.WorldId == worldId && l.Name == name);
    }

    private static async Task SetLocationResourceAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        Guid locationId,
        string resourceType,
        double quantity)
    {
        await using var db = await factory.CreateDbContextAsync();
        var resource = await db.LocationResources
            .FirstOrDefaultAsync(lr => lr.LocationId == locationId && lr.ResourceType == resourceType);
        if (resource is null)
        {
            db.LocationResources.Add(new LocationResource
            {
                LocationId = locationId,
                ResourceType = resourceType,
                Quantity = quantity,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            resource.Quantity = quantity;
            resource.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    private static async Task<Agent> SeedAgentAtAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        Guid worldId,
        string occupation,
        string locationName,
        double food = 0.0,
        double hunger = 0.2,
        double energy = 0.9,
        double health = 1.0,
        decimal money = 0m)
    {
        var agent = await TestSetup.SeedAgentAsync(factory, worldId,
            hunger: hunger, energy: energy, health: health);

        await using (var db = await factory.CreateDbContextAsync())
        {
            var tracked = await db.Agents.FirstAsync(a => a.Id == agent.Id);
            tracked.Occupation = occupation;
            tracked.Location = locationName;
            tracked.Money = money;
            await db.SaveChangesAsync();
        }

        if (food > 0)
        {
            await using var db = await factory.CreateDbContextAsync();
            db.AgentInventories.Add(new AgentInventory
            {
                AgentId = agent.Id,
                ResourceType = ResourceTypes.Food,
                Quantity = food,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        return agent;
    }

    private static async Task<ActionContext> BuildContextAsync(
        TestSetup.Harness harness,
        World world,
        Agent agent)
    {
        await using var db = await harness.DbContextFactory.CreateDbContextAsync();

        var locations = await db.Locations.AsNoTracking()
            .Where(l => l.WorldId == world.Id)
            .ToListAsync();

        var locationsById = locations.ToDictionary(l => l.Id);
        var locationsByName = locations.ToDictionary(l => l.Name);

        var resources = await db.LocationResources.AsNoTracking()
            .Where(lr => locations.Select(l => l.Id).Contains(lr.LocationId))
            .ToListAsync();
        var resourceDict = resources.ToDictionary(lr => (lr.LocationId, lr.ResourceType));

        var inventories = await db.AgentInventories.AsNoTracking()
            .Where(ai => ai.AgentId == agent.Id)
            .ToListAsync();
        var invDict = inventories.ToDictionary(ai => (ai.AgentId, ai.ResourceType));

        var reloadedAgent = await db.Agents.AsNoTracking().FirstAsync(a => a.Id == agent.Id);

        var currentLocation = locationsByName[reloadedAgent.Location];

        return new ActionContext(
            agent: reloadedAgent,
            world: world,
            simulation: new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
                world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1),
            currentLocation: currentLocation,
            locationsById: locationsById,
            locationsByName: locationsByName,
            locationResources: resourceDict,
            agentInventories: invDict,
            nearbyAgents: new List<Agent>(),
            newEvents: new List<SimulationEvent>(),
            pendingNewInventories: new List<AgentInventory>(),
            pendingNewLocationResources: new List<LocationResource>(),
            now: DateTime.UtcNow);
    }

    private static async Task SaveContextAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        ActionContext ctx)
    {
        await using var db = await factory.CreateDbContextAsync();

        foreach (var inv in ctx.AgentInventories.Values)
        {
            var existing = await db.AgentInventories
                .FirstOrDefaultAsync(ai => ai.AgentId == inv.AgentId && ai.ResourceType == inv.ResourceType);
            if (existing is null)
            {
                db.AgentInventories.Add(inv);
            }
            else
            {
                existing.Quantity = inv.Quantity;
                existing.UpdatedAt = inv.UpdatedAt;
            }
        }

        foreach (var res in ctx.LocationResources.Values)
        {
            var existing = await db.LocationResources
                .FirstOrDefaultAsync(lr => lr.LocationId == res.LocationId && lr.ResourceType == res.ResourceType);
            if (existing is null)
            {
                db.LocationResources.Add(res);
            }
            else
            {
                existing.Quantity = res.Quantity;
                existing.UpdatedAt = res.UpdatedAt;
            }
        }

        await db.SaveChangesAsync();
    }
}