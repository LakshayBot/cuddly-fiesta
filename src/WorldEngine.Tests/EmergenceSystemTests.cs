using Microsoft.EntityFrameworkCore;
using WorldEngine.Domain;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;
using WorldEngine.Infrastructure.Simulation;

namespace WorldEngine.Tests;

public class EmergenceSystemTests
{
    [Fact]
    public async Task Settlement_Forms_WhenClusterPersistsWithFood()
    {
        var options = DefaultOptions();
        options.MinSettlementPopulation = 8;
        options.SettlementPersistenceDays = 2.0;

        var harness = TestSetup.CreateHarness(options: options);
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var farm = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farm.Id, ResourceTypes.Food, 20.0);

        var agents = new List<Agent>();
        for (var i = 0; i < 10; i++)
        {
            var a = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
            agents.Add(a);
        }

        var simTime = world.CurrentSimulationTime;
        for (var day = 1; day <= 3; day++)
        {
            var nextTime = simTime.AddDays(day);
            var ctx = new SimulationContext(
                world,
                harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
                simTime, nextTime, day);
            await harness.SettlementSystem.ProcessAsync(ctx, CancellationToken.None);
            simTime = nextTime;
        }

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var settlements = await db.Settlements.AsNoTracking().Where(s => s.WorldId == world.Id).ToListAsync();
        Assert.Single(settlements);
        var settlement = settlements[0];
        Assert.Equal(LocationTypes.Farm, settlement.CenterLocationName);
        Assert.Equal(10, settlement.Population);
        Assert.Contains("10 agents lived within Farm", settlement.FormationReason);
        Assert.Contains("2 simulation days", settlement.FormationReason);

        var formedEvent = await db.SimulationEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.WorldId == world.Id && e.EventType == SimulationEventTypes.SettlementFormed);
        Assert.NotNull(formedEvent);
    }

    [Fact]
    public async Task Settlement_DoesNotForm_BelowPopulationThreshold()
    {
        var options = DefaultOptions();
        options.MinSettlementPopulation = 8;
        options.SettlementPersistenceDays = 1.0;

        var harness = TestSetup.CreateHarness(options: options);
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var farm = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farm.Id, ResourceTypes.Food, 20.0);

        for (var i = 0; i < 4; i++)
        {
            await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        }

        var simTime = world.CurrentSimulationTime;
        for (var day = 1; day <= 3; day++)
        {
            var nextTime = simTime.AddDays(day);
            var ctx = new SimulationContext(
                world,
                harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
                simTime, nextTime, day);
            await harness.SettlementSystem.ProcessAsync(ctx, CancellationToken.None);
            simTime = nextTime;
        }

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var settlements = await db.Settlements.AsNoTracking().Where(s => s.WorldId == world.Id).ToListAsync();
        Assert.Empty(settlements);
    }

    [Fact]
    public async Task Settlement_DoesNotForm_WithoutFood()
    {
        var options = DefaultOptions();
        options.MinSettlementPopulation = 8;
        options.SettlementPersistenceDays = 1.0;

        var harness = TestSetup.CreateHarness(options: options);
        var world = await SeedWorldAsync(harness.DbContextFactory);

        for (var i = 0; i < 10; i++)
        {
            await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        }

        var simTime = world.CurrentSimulationTime;
        for (var day = 1; day <= 3; day++)
        {
            var nextTime = simTime.AddDays(day);
            var ctx = new SimulationContext(
                world,
                harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
                simTime, nextTime, day);
            await harness.SettlementSystem.ProcessAsync(ctx, CancellationToken.None);
            simTime = nextTime;
        }

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var settlements = await db.Settlements.AsNoTracking().Where(s => s.WorldId == world.Id).ToListAsync();
        Assert.Empty(settlements);
    }

    [Fact]
    public async Task Settlement_DoesNotDuplicate()
    {
        var options = DefaultOptions();
        options.MinSettlementPopulation = 8;
        options.SettlementPersistenceDays = 1.0;

        var harness = TestSetup.CreateHarness(options: options);
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var farm = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farm.Id, ResourceTypes.Food, 20.0);

        for (var i = 0; i < 10; i++)
        {
            await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        }

        var simTime = world.CurrentSimulationTime;
        for (var day = 1; day <= 6; day++)
        {
            var nextTime = simTime.AddDays(day);
            var ctx = new SimulationContext(
                world,
                harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
                simTime, nextTime, day);
            await harness.SettlementSystem.ProcessAsync(ctx, CancellationToken.None);
            simTime = nextTime;
        }

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var settlements = await db.Settlements.AsNoTracking().Where(s => s.WorldId == world.Id).ToListAsync();
        Assert.Single(settlements);
    }

    [Fact]
    public async Task Group_Forms_FromMutualAffectionCluster()
    {
        var options = DefaultOptions();
        options.FamilyAffectionThreshold = 0.6;

        var harness = TestSetup.CreateHarness(options: options);
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var agents = new List<Agent>();
        for (var i = 0; i < 3; i++)
        {
            agents.Add(await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Village));
        }

        await SeedMutualRelationshipsAsync(harness.DbContextFactory, agents, affection: 0.9, trust: 0.9);

        var ctx = new SimulationContext(
            world,
            harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);
        await harness.GroupSystem.ProcessAsync(ctx, CancellationToken.None);

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var groups = await db.Groups.AsNoTracking().Where(g => g.WorldId == world.Id).ToListAsync();
        var family = groups.FirstOrDefault(g => g.Type == "Family");
        Assert.NotNull(family);

        var members = await db.GroupMemberships.AsNoTracking()
            .Where(m => m.GroupId == family!.Id)
            .Select(m => m.AgentId)
            .ToListAsync();
        Assert.Equal(3, members.Count);
        Assert.All(agents, a => Assert.Contains(a.Id, members));

        var formedEvent = await db.SimulationEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.WorldId == world.Id && e.EventType == SimulationEventTypes.GroupFormed);
        Assert.NotNull(formedEvent);
    }

    [Fact]
    public async Task Group_WorkGroup_FromSameOccupationAndLocation()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);

        for (var i = 0; i < 5; i++)
        {
            var a = await TestSetup.SeedAgentAsync(harness.DbContextFactory, world.Id);
            await using (var dbSeed = await harness.DbContextFactory.CreateDbContextAsync())
            {
                var tracked = await dbSeed.Agents.FirstAsync(x => x.Id == a.Id);
                tracked.Occupation = Occupations.Farmer;
                tracked.Location = LocationTypes.Farm;
                await dbSeed.SaveChangesAsync();
            }
        }

        var ctx = new SimulationContext(
            world,
            harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);
        await harness.GroupSystem.ProcessAsync(ctx, CancellationToken.None);

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var workGroup = await db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.WorldId == world.Id && g.Type == "WorkGroup");
        Assert.NotNull(workGroup);
    }

    [Fact]
    public async Task Group_DoesNotDuplicateExistingMembers()
    {
        var options = DefaultOptions();
        options.FamilyAffectionThreshold = 0.6;

        var harness = TestSetup.CreateHarness(options: options);
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var agents = new List<Agent>();
        for (var i = 0; i < 3; i++)
        {
            agents.Add(await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Village));
        }
        await SeedMutualRelationshipsAsync(harness.DbContextFactory, agents, affection: 0.9, trust: 0.9);

        for (var i = 0; i < 3; i++)
        {
            var ctx = new SimulationContext(
                world,
                harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
                world.CurrentSimulationTime.AddSeconds(i * 60),
                world.CurrentSimulationTime.AddSeconds((i + 1) * 60),
                i + 1);
            await harness.GroupSystem.ProcessAsync(ctx, CancellationToken.None);
        }

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var families = await db.Groups.AsNoTracking()
            .Where(g => g.WorldId == world.Id && g.Type == "Family")
            .ToListAsync();
        Assert.Single(families);
    }

    [Fact]
    public async Task Trade_TransfersFoodAndMoney()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var seller = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Village, food: 5.0, money: 1m);
        var buyer = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Village, money: 3m);

        var ctx = await BuildTradeContextAsync(harness, world, seller, buyer);
        var reloadedBuyer = ctx.NearbyAgents.Single();

        var trade = new WorldEngine.Domain.Actions.TradeAction(reloadedBuyer, ResourceTypes.Food, TradeDefaults.FoodPrice);
        Assert.True(trade.IsAvailable(ctx));
        trade.Execute(ctx);

        Assert.Equal(4.0, ctx.GetInventoryQuantity(ResourceTypes.Food), 6);
        Assert.Equal(1m + TradeDefaults.FoodPrice, ctx.Agent.Money);
        Assert.Equal(3m - TradeDefaults.FoodPrice, reloadedBuyer.Money);
        Assert.Contains(ctx.NewEvents, e => e.EventType == SimulationEventTypes.AgentTraded);
    }

    [Fact]
    public async Task Trade_NotAvailable_WhenBuyerLacksMoney()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var seller = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Village, food: 5.0, money: 1m);
        var poorBuyer = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Village, money: 0m);

        var ctx = await BuildTradeContextAsync(harness, world, seller, poorBuyer);
        var reloadedPoorBuyer = ctx.NearbyAgents.Single();

        var trade = new WorldEngine.Domain.Actions.TradeAction(reloadedPoorBuyer, ResourceTypes.Food, TradeDefaults.FoodPrice);
        Assert.False(trade.IsAvailable(ctx));
    }

    [Fact]
    public async Task Conflict_Detected_WhenAngerAndScarcity()
    {
        var options = DefaultOptions();
        options.ConflictAngerThreshold = 0.6;
        options.ConflictHungerThreshold = 0.8;
        options.ScarcityThreshold = 0.2;
        options.ConflictCooldownTicks = 10;

        var harness = TestSetup.CreateHarness(options: options);
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var farm = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farm.Id, ResourceTypes.Food, 0.0);

        var source = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        var target = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);

        await using (var dbSeed = await harness.DbContextFactory.CreateDbContextAsync())
        {
            dbSeed.AgentRelationships.Add(new AgentRelationship
            {
                SourceAgentId = source.Id,
                TargetAgentId = target.Id,
                Trust = 0.3,
                Affection = 0.3,
                Respect = 0.4,
                Fear = 0.1,
                Anger = 0.9,
                Familiarity = 0.5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await dbSeed.SaveChangesAsync();
        }

        var ctx = new SimulationContext(
            world,
            harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);
        await harness.ConflictSystem.ProcessAsync(ctx, CancellationToken.None);

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var conflict = await db.SimulationEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.WorldId == world.Id && e.EventType == SimulationEventTypes.ConflictOccurred);
        Assert.NotNull(conflict);
        Assert.Equal(source.Id, conflict!.ActorAgentId);
        Assert.Equal(target.Id, conflict.TargetAgentId);
        Assert.Contains("ResourceScarcity", conflict.Data);
    }

    [Fact]
    public async Task Conflict_NotDetected_WhenAngerLow()
    {
        var options = DefaultOptions();
        options.ConflictAngerThreshold = 0.6;

        var harness = TestSetup.CreateHarness(options: options);
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var farm = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farm.Id, ResourceTypes.Food, 0.0);

        var source = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        var target = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);

        await using (var dbSeed = await harness.DbContextFactory.CreateDbContextAsync())
        {
            dbSeed.AgentRelationships.Add(new AgentRelationship
            {
                SourceAgentId = source.Id,
                TargetAgentId = target.Id,
                Trust = 0.5,
                Affection = 0.5,
                Respect = 0.5,
                Fear = 0.0,
                Anger = 0.1,
                Familiarity = 0.2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await dbSeed.SaveChangesAsync();
        }

        var ctx = new SimulationContext(
            world,
            harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);
        await harness.ConflictSystem.ProcessAsync(ctx, CancellationToken.None);

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var conflicts = await db.SimulationEvents.AsNoTracking()
            .Where(e => e.WorldId == world.Id && e.EventType == SimulationEventTypes.ConflictOccurred)
            .ToListAsync();
        Assert.Empty(conflicts);
    }

    [Fact]
    public async Task Conflict_RespectsCooldown()
    {
        var options = DefaultOptions();
        options.ConflictAngerThreshold = 0.6;
        options.ScarcityThreshold = 0.2;
        options.ConflictCooldownTicks = 100;

        var harness = TestSetup.CreateHarness(options: options);
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var farm = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farm.Id, ResourceTypes.Food, 0.0);

        var source = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        var target = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);

        await using (var dbSeed = await harness.DbContextFactory.CreateDbContextAsync())
        {
            dbSeed.AgentRelationships.Add(new AgentRelationship
            {
                SourceAgentId = source.Id,
                TargetAgentId = target.Id,
                Trust = 0.3,
                Affection = 0.3,
                Respect = 0.4,
                Fear = 0.1,
                Anger = 0.9,
                Familiarity = 0.5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await dbSeed.SaveChangesAsync();
        }

        for (var tick = 1; tick <= 5; tick++)
        {
            var ctx = new SimulationContext(
                world,
                harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
                world.CurrentSimulationTime.AddSeconds((tick - 1) * 60),
                world.CurrentSimulationTime.AddSeconds(tick * 60),
                tick);
            await harness.ConflictSystem.ProcessAsync(ctx, CancellationToken.None);
        }

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var conflicts = await db.SimulationEvents.AsNoTracking()
            .Where(e => e.WorldId == world.Id && e.EventType == SimulationEventTypes.ConflictOccurred)
            .ToListAsync();
        Assert.Single(conflicts);
    }

    [Fact]
    public async Task Steal_TransfersFoodFromTargetToStarvingActor()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var starvingActor = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Village);
        var wealthyTarget = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Village, food: 5.0);

        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            var tracked = await db.Agents.FirstAsync(a => a.Id == starvingActor.Id);
            tracked.Hunger = 0.95;
            await db.SaveChangesAsync();
        }

        var ctx = await BuildTradeContextAsync(harness, world, starvingActor, wealthyTarget);
        var reloadedTarget = ctx.NearbyAgents.Single();

        var steal = new WorldEngine.Domain.Actions.StealAction(reloadedTarget);
        Assert.True(steal.IsAvailable(ctx));
        steal.Execute(ctx);

        Assert.Equal(1.0, ctx.GetInventoryQuantity(ResourceTypes.Food), 6);
        Assert.Equal(4.0, ctx.GetOtherAgentInventoryQuantity(reloadedTarget.Id, ResourceTypes.Food), 6);
        Assert.Contains(ctx.NewEvents, e => e.EventType == SimulationEventTypes.AgentStole);
    }

    [Fact]
    public async Task Conflict_Emerges_AfterStealRaisesAnger()
    {
        var options = DefaultOptions();
        options.ConflictAngerThreshold = 0.6;
        options.ScarcityThreshold = 0.2;
        options.ConflictCooldownTicks = 10;

        var harness = TestSetup.CreateHarness(options: options);
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var farm = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationResourceAsync(harness.DbContextFactory, farm.Id, ResourceTypes.Food, 0.0);

        var victim = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm, food: 5.0);
        var thief = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);

        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            var tracked = await db.Agents.FirstAsync(a => a.Id == thief.Id);
            tracked.Hunger = 0.95;
            await db.SaveChangesAsync();
        }

        // Victim -> Thief relationship with high anger (theft consequence)
        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            db.AgentRelationships.Add(new AgentRelationship
            {
                SourceAgentId = victim.Id,
                TargetAgentId = thief.Id,
                Trust = 0.2,
                Affection = 0.2,
                Respect = 0.4,
                Fear = 0.1,
                Anger = 0.9,
                Familiarity = 0.5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var ctx = new SimulationContext(
            world,
            harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);
        await harness.ConflictSystem.ProcessAsync(ctx, CancellationToken.None);

        await using var dbVerify = await harness.DbContextFactory.CreateDbContextAsync();
        var conflict = await dbVerify.SimulationEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.WorldId == world.Id && e.EventType == SimulationEventTypes.ConflictOccurred);
        Assert.NotNull(conflict);
        Assert.Equal(victim.Id, conflict!.ActorAgentId);
        Assert.Equal(thief.Id, conflict.TargetAgentId);
    }

    private static SimulationOptions DefaultOptions() => new();

    private static async Task<World> SeedWorldAsync(IDbContextFactory<WorldEngineDbContext> factory)
    {
        return await TestSetup.SeedWorldAsync(factory, SimulationStatus.Running, speed: 1.0);
    }

    private static async Task<Agent> SeedAgentAtAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        Guid worldId,
        string location,
        double food = 0.0,
        decimal money = 5m)
    {
        var agent = await TestSetup.SeedAgentAsync(factory, worldId);
        await using (var db = await factory.CreateDbContextAsync())
        {
            var tracked = await db.Agents.FirstAsync(a => a.Id == agent.Id);
            tracked.Location = location;
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

    private static async Task SeedMutualRelationshipsAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        IReadOnlyList<Agent> agents,
        double affection,
        double trust)
    {
        await using var db = await factory.CreateDbContextAsync();
        foreach (var source in agents)
        {
            foreach (var target in agents)
            {
                if (source.Id == target.Id) continue;
                db.AgentRelationships.Add(new AgentRelationship
                {
                    SourceAgentId = source.Id,
                    TargetAgentId = target.Id,
                    Trust = trust,
                    Affection = affection,
                    Respect = 0.5,
                    Fear = 0.0,
                    Anger = 0.0,
                    Familiarity = 0.5,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
        }
        await db.SaveChangesAsync();
    }

    private static async Task<WorldEngine.Domain.Actions.ActionContext> BuildTradeContextAsync(
        TestSetup.Harness harness,
        World world,
        Agent seller,
        Agent buyer)
    {
        await using var db = await harness.DbContextFactory.CreateDbContextAsync();

        var locations = await db.Locations.AsNoTracking().Where(l => l.WorldId == world.Id).ToListAsync();
        var locationsById = locations.ToDictionary(l => l.Id);
        var locationsByName = locations.ToDictionary(l => l.Name);

        var inventories = await db.AgentInventories.AsNoTracking()
            .Where(ai => ai.AgentId == seller.Id || ai.AgentId == buyer.Id)
            .ToListAsync();
        var invDict = inventories.ToDictionary(ai => (ai.AgentId, ai.ResourceType));

        var resources = await db.LocationResources.AsNoTracking().ToListAsync();
        var resourceDict = resources.ToDictionary(lr => (lr.LocationId, lr.ResourceType));

        var reloadedSeller = await db.Agents.AsNoTracking().FirstAsync(a => a.Id == seller.Id);
        var reloadedBuyer = await db.Agents.AsNoTracking().FirstAsync(a => a.Id == buyer.Id);
        var currentLocation = locationsByName[reloadedSeller.Location];

        var sim = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);

        return new WorldEngine.Domain.Actions.ActionContext(
            agent: reloadedSeller,
            world: world,
            simulation: sim,
            currentLocation: currentLocation,
            locationsById: locationsById,
            locationsByName: locationsByName,
            locationResources: resourceDict,
            agentInventories: invDict,
            nearbyAgents: new List<Agent> { reloadedBuyer },
            newEvents: new List<SimulationEvent>(),
            pendingNewInventories: new List<AgentInventory>(),
            pendingNewLocationResources: new List<LocationResource>(),
            now: DateTime.UtcNow);
    }
}