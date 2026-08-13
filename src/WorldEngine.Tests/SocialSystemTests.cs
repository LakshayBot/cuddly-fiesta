using Microsoft.EntityFrameworkCore;
using WorldEngine.Domain;
using WorldEngine.Domain.Actions;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Tests;

public class SocialSystemTests
{
    [Fact]
    public async Task Help_IncreasesPositiveRelationshipMetrics()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var actor = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village,
            food: 5.0, hunger: 0.2, energy: 0.9, generosity: 0.7);
        var target = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village,
            food: 0.0, hunger: 0.85, energy: 0.6);

        var ctx = await BuildContextAsync(harness, world, actor, nearbyAgents: new[] { target });

        new HelpAction(target).Execute(ctx);

        await SaveContextAsync(harness.DbContextFactory, ctx);

        var helpEvent = ctx.NewEvents.Single(e => e.EventType == SimulationEventTypes.AgentHelped);
        var simContext = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);
        await harness.SocialSystem.ProcessAsync(simContext, CancellationToken.None);

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var rel = await db.AgentRelationships.AsNoTracking()
            .FirstAsync(r => r.SourceAgentId == target.Id && r.TargetAgentId == actor.Id);

        Assert.Equal(RelationshipDefaults.Trust + RelationshipDeltas.HelpTrust, rel.Trust, 6);
        Assert.Equal(RelationshipDefaults.Affection + RelationshipDeltas.HelpAffection, rel.Affection, 6);
        Assert.Equal(RelationshipDefaults.Familiarity + RelationshipDeltas.HelpFamiliarity, rel.Familiarity, 6);

        var memory = await db.AgentMemories.AsNoTracking()
            .FirstOrDefaultAsync(m => m.AgentId == target.Id && m.SimulationEventId == helpEvent.Id);
        Assert.NotNull(memory);
        Assert.True(memory!.Importance >= MemoryImportance.Notable);
    }

    [Fact]
    public async Task DirectInsert_Relationship_Persists()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var actor = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village);
        var target = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village);

        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            db.AgentRelationships.Add(new AgentRelationship
            {
                SourceAgentId = target.Id,
                TargetAgentId = actor.Id,
                Trust = 0.7,
                Affection = 0.5,
                Respect = 0.5,
                Fear = 0.0,
                Anger = 0.0,
                Familiarity = 0.1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var verify = await harness.DbContextFactory.CreateDbContextAsync())
        {
            var rel = await verify.AgentRelationships.AsNoTracking()
                .FirstOrDefaultAsync(r => r.SourceAgentId == target.Id && r.TargetAgentId == actor.Id);
            Assert.NotNull(rel);
            Assert.Equal(0.7, rel!.Trust, 6);
        }
    }

    [Fact]
    public async Task Steal_DecreasesTrust()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var actor = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village);
        var target = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village);

        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            db.SimulationEvents.Add(new SimulationEvent
            {
                Id = Guid.NewGuid(),
                WorldId = world.Id,
                Tick = 1,
                SimulationTime = world.CurrentSimulationTime,
                EventType = SimulationEventTypes.AgentStole,
                ActorAgentId = actor.Id,
                TargetAgentId = target.Id,
                LocationId = null,
                Data = "{}",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var simContext = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);
        await harness.SocialSystem.ProcessAsync(simContext, CancellationToken.None);

        await using var verify = await harness.DbContextFactory.CreateDbContextAsync();
        var allRel = await verify.AgentRelationships.AsNoTracking().ToListAsync();
        var allMem = await verify.AgentMemories.AsNoTracking().ToListAsync();
        Assert.True(allRel.Count > 0 || allMem.Count > 0,
            $"allRel={allRel.Count} allMem={allMem.Count}");

        var rel = allRel.FirstOrDefault(r => r.SourceAgentId == target.Id && r.TargetAgentId == actor.Id);
        Assert.NotNull(rel);
        Assert.True(rel!.Trust < RelationshipDefaults.Trust);
        Assert.Equal(RelationshipDefaults.Trust + RelationshipDeltas.StealTrust, rel.Trust, 6);
    }

    [Fact]
    public async Task MeaningfulEvents_CreateMemories()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var actor = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village, food: 5.0);
        var target = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village, hunger: 0.85);

        var ctx = await BuildContextAsync(harness, world, actor, nearbyAgents: new[] { target });
        new HelpAction(target).Execute(ctx);
        await SaveContextAsync(harness.DbContextFactory, ctx);

        var helpEvent = ctx.NewEvents.Single(e => e.EventType == SimulationEventTypes.AgentHelped);
        var simContext = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);
        await harness.SocialSystem.ProcessAsync(simContext, CancellationToken.None);

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var targetMemories = await db.AgentMemories.AsNoTracking()
            .Where(m => m.AgentId == target.Id && m.SimulationEventId == helpEvent.Id)
            .ToListAsync();
        var actorMemories = await db.AgentMemories.AsNoTracking()
            .Where(m => m.AgentId == actor.Id && m.SimulationEventId == helpEvent.Id)
            .ToListAsync();

        Assert.Single(targetMemories);
        Assert.Single(actorMemories);
        Assert.Equal(MemoryTypes.ReceivedHelp, targetMemories[0].Type);
        Assert.Equal(MemoryTypes.HelpedSomeone, actorMemories[0].Type);
    }

    [Fact]
    public async Task MinorEvents_DoNotCreateExcessiveMemories()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var actor = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village);
        var target = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village);

        for (var i = 0; i < 20; i++)
        {
            var simContext = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
                world.CurrentSimulationTime.AddSeconds(i * 60), world.CurrentSimulationTime.AddSeconds((i + 1) * 60), i + 1);

            await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
            {
                db.SimulationEvents.Add(new SimulationEvent
                {
                    Id = Guid.NewGuid(),
                    WorldId = world.Id,
                    Tick = simContext.TickNumber,
                    SimulationTime = simContext.NewSimulationTime,
                    EventType = SimulationEventTypes.AgentTalked,
                    ActorAgentId = actor.Id,
                    TargetAgentId = target.Id,
                    LocationId = null,
                    Data = "{}",
                    CreatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            await harness.SocialSystem.ProcessAsync(simContext, CancellationToken.None);
        }

        await using var verify = await harness.DbContextFactory.CreateDbContextAsync();
        var talkMemories = await verify.AgentMemories.AsNoTracking()
            .Where(m => m.AgentId == target.Id && m.Type == MemoryTypes.Talked)
            .ToListAsync();

        Assert.Equal(20, talkMemories.Count);
        Assert.All(talkMemories, m => Assert.True(m.Importance <= MemoryImportance.Minor + 0.001));
    }

    [Fact]
    public async Task Relationships_AreDirectional()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var actor = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village, food: 5.0);
        var target = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village, hunger: 0.85);

        var ctx = await BuildContextAsync(harness, world, actor, nearbyAgents: new[] { target });
        new HelpAction(target).Execute(ctx);
        await SaveContextAsync(harness.DbContextFactory, ctx);

        var simContext = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);
        await harness.SocialSystem.ProcessAsync(simContext, CancellationToken.None);

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var forwardRel = await db.AgentRelationships.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SourceAgentId == target.Id && r.TargetAgentId == actor.Id);
        var reverseRel = await db.AgentRelationships.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SourceAgentId == actor.Id && r.TargetAgentId == target.Id);

        Assert.NotNull(forwardRel);
        Assert.True(forwardRel!.Trust > RelationshipDefaults.Trust);
        Assert.Null(reverseRel);
    }

    [Fact]
    public async Task DeadAgents_DoNotInitiateInteractions()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var deadAgent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village,
            alive: false);
        var aliveAgent = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village,
            food: 0.0, hunger: 0.85);

        var ctx = await BuildContextAsync(harness, world, deadAgent, nearbyAgents: new[] { aliveAgent });

        var talk = new TalkAction(aliveAgent);
        Assert.False(talk.IsAvailable(ctx));

        var help = new HelpAction(aliveAgent);
        Assert.False(help.IsAvailable(ctx));

        var share = new ShareFoodAction(aliveAgent);
        Assert.False(share.IsAvailable(ctx));
    }

    [Fact]
    public async Task DeadTargets_BlockSocialActions()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var aliveActor = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village, food: 5.0);
        var deadTarget = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village,
            alive: false);

        var ctx = await BuildContextAsync(harness, world, aliveActor, nearbyAgents: new[] { deadTarget });

        var talk = new TalkAction(deadTarget);
        Assert.False(talk.IsAvailable(ctx));

        var help = new HelpAction(deadTarget);
        Assert.False(help.IsAvailable(ctx));

        var share = new ShareFoodAction(deadTarget);
        Assert.False(share.IsAvailable(ctx));
    }

    [Fact]
    public async Task TalkAction_RequiresSameLocation()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var actor = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village);
        var distantTarget = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Farm);

        var ctx = await BuildContextAsync(harness, world, actor, nearbyAgents: new[] { distantTarget });
        var reloadedTarget = ctx.NearbyAgents.Single();

        var talk = new TalkAction(reloadedTarget);
        Assert.False(talk.IsAvailable(ctx));
    }

    [Fact]
    public async Task RepeatedHelp_AccumulatesTrust()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var actor = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village, food: 5.0);
        var target = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village, hunger: 0.85);

        for (var i = 0; i < 3; i++)
        {
            var tick = i + 1;
            var ctx = await BuildContextAsync(harness, world, actor, nearbyAgents: new[] { target }, tickNumber: tick);
            new HelpAction(target).Execute(ctx);
            await SaveContextAsync(harness.DbContextFactory, ctx);

            var simContext = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
                world.CurrentSimulationTime.AddSeconds(i * 60), world.CurrentSimulationTime.AddSeconds((i + 1) * 60), tick);
            await harness.SocialSystem.ProcessAsync(simContext, CancellationToken.None);
        }

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var rel = await db.AgentRelationships.AsNoTracking()
            .FirstAsync(r => r.SourceAgentId == target.Id && r.TargetAgentId == actor.Id);

        Assert.Equal(
            RelationshipDefaults.Trust + (RelationshipDeltas.HelpTrust * 3),
            rel.Trust,
            6);
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
        decimal money = 0m,
        bool alive = true,
        double generosity = 0.5)
    {
        var agent = await TestSetup.SeedAgentAsync(factory, worldId,
            hunger: hunger, energy: energy, health: health, alive: alive);

        await using (var db = await factory.CreateDbContextAsync())
        {
            var tracked = await db.Agents.FirstAsync(a => a.Id == agent.Id);
            tracked.Occupation = occupation;
            tracked.Location = locationName;
            tracked.Money = money;
            tracked.Generosity = generosity;
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
        Agent agent,
        IEnumerable<Agent> nearbyAgents,
        long tickNumber = 1)
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

        var nearby = nearbyAgents.Select(a => db.Agents.AsNoTracking().First(x => x.Id == a.Id)).ToList();

        return new ActionContext(
            agent: reloadedAgent,
            world: world,
            simulation: new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
                world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), tickNumber),
            currentLocation: currentLocation,
            locationsById: locationsById,
            locationsByName: locationsByName,
            locationResources: resourceDict,
            agentInventories: invDict,
            nearbyAgents: nearby,
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

        if (ctx.NewEvents.Count > 0)
        {
            db.SimulationEvents.AddRange(ctx.NewEvents);
        }

        await db.SaveChangesAsync();
    }
}