using Microsoft.EntityFrameworkCore;
using WorldEngine.Domain;
using WorldEngine.Domain.AI;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Tests;

public class DeepSimulationTests
{
    [Fact]
    public async Task DecisionFactors_IncludeHungerAndBaseline()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var agent = await SeedAgentAsync(harness.DbContextFactory, world.Id,
            hunger: 0.95, energy: 0.9, food: 3.0);

        var ctx = await BuildDecisionContextAsync(harness, world, agent);
        var engine = new RuleBasedDecisionEngine(new ActionGenerator());

        var decision = await engine.DecideAsync(ctx, CancellationToken.None);

        Assert.NotNull(decision.SelectedAction);
        Assert.Equal(ActionTypes.Eat, decision.SelectedAction!.ActionType);
        Assert.NotNull(decision.SelectedAction.Factors);
        Assert.Contains(decision.SelectedAction.Factors!, f => f.Name == "Hunger" && f.Contribution > 0);
        Assert.Contains(decision.SelectedAction.Factors!, f => f.Name == "HungerPressure" && f.Contribution > 0);
    }

    [Fact]
    public async Task DecisionFactors_PersonalityAffectsHelpScore()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var generous = await SeedAgentAsync(harness.DbContextFactory, world.Id,
            hunger: 0.2, energy: 0.9, food: 5.0, generosity: 0.95);
        var target = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, hunger: 0.9);

        var ctx = await BuildDecisionContextAsync(harness, world, generous, nearby: new[] { target });
        var engine = new RuleBasedDecisionEngine(new ActionGenerator());

        var decision = await engine.DecideAsync(ctx, CancellationToken.None);
        var help = decision.AvailableActions.First(a => a.ActionType == ActionTypes.Help);

        Assert.NotNull(help.Factors);
        Assert.Contains(help.Factors!, f => f.Name == "Generosity");
        Assert.Contains(help.Factors!, f => f.Name == "TargetHunger");
    }

    [Fact]
    public async Task DecisionFactors_RelationshipAndMemoryAffectHelp()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var agent = await SeedAgentAsync(harness.DbContextFactory, world.Id,
            hunger: 0.2, energy: 0.9, food: 5.0, generosity: 0.7);
        var target = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, hunger: 0.9);

        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            db.AgentRelationships.Add(new AgentRelationship
            {
                SourceAgentId = agent.Id,
                TargetAgentId = target.Id,
                Trust = 0.9,
                Affection = 0.9,
                Respect = 0.5,
                Fear = 0.0,
                Anger = 0.0,
                Familiarity = 0.5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            db.AgentMemories.Add(new AgentMemory
            {
                Id = Guid.NewGuid(),
                AgentId = agent.Id,
                SimulationEventId = Guid.NewGuid(),
                Type = MemoryTypes.ReceivedHelp,
                Importance = 0.8,
                EmotionalImpact = 0.4,
                CreatedSimulationTime = world.CurrentSimulationTime.AddDays(-5),
                OtherAgentId = target.Id,
                Summary = "They helped me during a famine.",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var rel = new AgentRelationship
        {
            SourceAgentId = agent.Id,
            TargetAgentId = target.Id,
            Trust = 0.9,
            Affection = 0.9,
            Respect = 0.5,
            Fear = 0.0,
            Anger = 0.0,
            Familiarity = 0.5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var mem = new AgentMemory
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            SimulationEventId = Guid.NewGuid(),
            Type = MemoryTypes.ReceivedHelp,
            Importance = 0.8,
            EmotionalImpact = 0.4,
            CreatedSimulationTime = world.CurrentSimulationTime.AddDays(-5),
            OtherAgentId = target.Id,
            Summary = "They helped me during a famine.",
            CreatedAt = DateTime.UtcNow,
        };

        var ctx = await BuildDecisionContextAsync(
            harness, world, agent,
            nearby: new[] { target },
            relationships: new[] { rel },
            memories: new[] { mem });
        var engine = new RuleBasedDecisionEngine(new ActionGenerator());

        var decision = await engine.DecideAsync(ctx, CancellationToken.None);
        var help = decision.AvailableActions.First(a => a.ActionType == ActionTypes.Help);

        Assert.NotNull(help.Factors);
        Assert.Contains(help.Factors!, f => f.Type == FactorType.Relationship && f.Contribution > 0);
        Assert.Contains(help.Factors!, f => f.Type == FactorType.Memory && f.Contribution > 0);
    }

    [Fact]
    public async Task Needs_InfluenceScoring()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var hungry = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.98, energy: 0.9, food: 5.0);
        var ctx = await BuildDecisionContextAsync(harness, world, hungry);
        var engine = new RuleBasedDecisionEngine(new ActionGenerator());
        var decision = await engine.DecideAsync(ctx, CancellationToken.None);
        Assert.Equal(ActionTypes.Eat, decision.SelectedAction!.ActionType);

        var tired = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.2, energy: 0.02);
        ctx = await BuildDecisionContextAsync(harness, world, tired);
        decision = await engine.DecideAsync(ctx, CancellationToken.None);
        Assert.Equal(ActionTypes.Rest, decision.SelectedAction!.ActionType);
    }

    [Fact]
    public async Task Causality_StealEvent_HasDecisionAndStateCauses()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var thief = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, hunger: 0.95, energy: 0.9);
        var victim = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, hunger: 0.2, food: 5.0);

        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            db.AgentRelationships.Add(new AgentRelationship
            {
                SourceAgentId = thief.Id,
                TargetAgentId = victim.Id,
                Trust = 0.3,
                Affection = 0.3,
                Respect = 0.5,
                Fear = 0.0,
                Anger = 0.0,
                Familiarity = 0.2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var sim = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);

        await harness.AgentSystem.ProcessAsync(sim, CancellationToken.None);
        await harness.CausalitySystem.ProcessAsync(sim, CancellationToken.None);

        await using var verify = await harness.DbContextFactory.CreateDbContextAsync();
        var stealEvent = await verify.SimulationEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.WorldId == world.Id && e.EventType == SimulationEventTypes.AgentStole);
        Assert.NotNull(stealEvent);

        var causes = await verify.EventCauses.AsNoTracking()
            .Where(c => c.EventId == stealEvent!.Id)
            .ToListAsync();
        Assert.Contains(causes, c => c.CauseType == EventCauseTypes.Decision);
        Assert.Contains(causes, c => c.Name == "Hunger" && c.CauseType == EventCauseTypes.State);
    }

    [Fact]
    public async Task Causality_HelpEvent_HasDirectMemoryConsequence()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var helper = await SeedAgentAsync(harness.DbContextFactory, world.Id,
            hunger: 0.2, energy: 0.9, food: 5.0, generosity: 0.9);
        var starving = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, hunger: 0.9);

        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            db.AgentRelationships.Add(new AgentRelationship
            {
                SourceAgentId = starving.Id,
                TargetAgentId = helper.Id,
                Trust = 0.8,
                Affection = 0.8,
                Respect = 0.5,
                Fear = 0.0,
                Anger = 0.0,
                Familiarity = 0.5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var sim = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);
        await harness.AgentSystem.ProcessAsync(sim, CancellationToken.None);
        await harness.SocialSystem.ProcessAsync(sim, CancellationToken.None);
        await harness.CausalitySystem.ProcessAsync(sim, CancellationToken.None);

        await using var verify = await harness.DbContextFactory.CreateDbContextAsync();
        var helpEvent = await verify.SimulationEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.WorldId == world.Id && e.EventType == SimulationEventTypes.AgentHelped);
        Assert.NotNull(helpEvent);

        var consequences = await verify.EventConsequences.AsNoTracking()
            .Where(c => c.EventId == helpEvent!.Id && c.Kind == EventConsequenceKinds.Direct)
            .ToListAsync();
        Assert.Contains(consequences, c => c.ConsequenceType == EventConsequenceTypes.MemoryCreated);
        Assert.Contains(consequences, c => c.ConsequenceType == EventConsequenceTypes.RelationshipChanged);

        var memories = await verify.AgentMemories.AsNoTracking()
            .Where(m => m.SimulationEventId == helpEvent.Id)
            .ToListAsync();
        Assert.NotEmpty(memories);
    }

    [Fact]
    public async Task Causality_TheftToDeath_TracesIndirectConsequence()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var thief = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, hunger: 0.95);
        var victim = await SeedAgentAtAsync(harness.DbContextFactory, world.Id, hunger: 0.9, food: 1.0);

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

        var sim = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);
        await harness.AgentSystem.ProcessAsync(sim, CancellationToken.None);
        await harness.SocialSystem.ProcessAsync(sim, CancellationToken.None);
        await harness.CausalitySystem.ProcessAsync(sim, CancellationToken.None);

        await using var db2 = await harness.DbContextFactory.CreateDbContextAsync();
        var theft = await db2.SimulationEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.WorldId == world.Id && e.EventType == SimulationEventTypes.AgentStole);
        Assert.NotNull(theft);

        var theftCauses = await db2.EventCauses.AsNoTracking()
            .Where(c => c.EventId == theft!.Id)
            .ToListAsync();
        Assert.NotEmpty(theftCauses);
    }

    [Fact]
    public async Task Causality_UnrelatedEvent_HasNoConnections()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var agent = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.2, energy: 0.9);

        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            var evt = new SimulationEvent
            {
                Id = Guid.NewGuid(),
                WorldId = world.Id,
                Tick = 1,
                SimulationTime = world.CurrentSimulationTime,
                EventType = SimulationEventTypes.AgentRested,
                ActorAgentId = agent.Id,
                TargetAgentId = null,
                LocationId = null,
                Data = "{}",
                CreatedAt = DateTime.UtcNow,
            };
            db.SimulationEvents.Add(evt);
            await db.SaveChangesAsync();
        }

        var sim = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);
        await harness.CausalitySystem.ProcessAsync(sim, CancellationToken.None);

        await using var verify = await harness.DbContextFactory.CreateDbContextAsync();
        var causes = await verify.EventCauses.AsNoTracking().ToListAsync();
        var consequences = await verify.EventConsequences.AsNoTracking().ToListAsync();
        Assert.Empty(causes);
        Assert.Empty(consequences);
    }

    [Fact]
    public async Task LifeHistory_ContainsSignificantEvents_NotTrivial()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var agent = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.2, energy: 0.9);

        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            db.SimulationEvents.AddRange(
                new SimulationEvent
                {
                    Id = Guid.NewGuid(),
                    WorldId = world.Id,
                    Tick = 10,
                    SimulationTime = world.CurrentSimulationTime.AddDays(10),
                    EventType = SimulationEventTypes.AgentStole,
                    ActorAgentId = agent.Id,
                    TargetAgentId = null,
                    LocationId = null,
                    Data = "{\"name\":\"X\"}",
                    Importance = EventImportance.Significant,
                    ImportanceScore = 25,
                    CreatedAt = DateTime.UtcNow,
                },
                new SimulationEvent
                {
                    Id = Guid.NewGuid(),
                    WorldId = world.Id,
                    Tick = 11,
                    SimulationTime = world.CurrentSimulationTime.AddDays(11),
                    EventType = SimulationEventTypes.AgentAte,
                    ActorAgentId = agent.Id,
                    TargetAgentId = null,
                    LocationId = null,
                    Data = "{}",
                    Importance = EventImportance.Trivial,
                    ImportanceScore = 2,
                    CreatedAt = DateTime.UtcNow,
                });
            await db.SaveChangesAsync();
        }

        var milestones = await BuildLifeMilestonesAsync(harness.DbContextFactory, world, agent.Id);

        Assert.Contains(milestones, m => m.Type == SimulationEventTypes.AgentStole);
        Assert.DoesNotContain(milestones, m => m.Type == SimulationEventTypes.AgentAte);
    }

    [Fact]
    public async Task LifeHistory_DeadAgent_RetainsHistory()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var agent = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.2, energy: 0.9);

        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            var tracked = await db.Agents.FirstAsync(a => a.Id == agent.Id);
            tracked.Alive = false;
            tracked.DeathSimulationTime = world.CurrentSimulationTime;
            tracked.DeathCause = "Old age";
            await db.SaveChangesAsync();

            db.SimulationEvents.Add(new SimulationEvent
            {
                Id = Guid.NewGuid(),
                WorldId = world.Id,
                Tick = 5,
                SimulationTime = world.CurrentSimulationTime.AddDays(5),
                EventType = SimulationEventTypes.AgentDied,
                ActorAgentId = null,
                TargetAgentId = agent.Id,
                LocationId = null,
                Data = "{\"name\":\"Mira\",\"cause\":\"Old age\",\"age\":80}",
                Importance = EventImportance.Significant,
                ImportanceScore = 30,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var milestones = await BuildLifeMilestonesAsync(harness.DbContextFactory, world, agent.Id);
        Assert.Contains(milestones, m => m.Type == SimulationEventTypes.AgentDied);
    }

    [Fact]
    public async Task Autopsy_TracesFoodDeclineToDeaths()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);

        var simTime = world.CurrentSimulationTime;
        for (var i = 0; i < 5; i++)
        {
            await using var db = await harness.DbContextFactory.CreateDbContextAsync();
            db.SimulationEvents.Add(new SimulationEvent
            {
                Id = Guid.NewGuid(),
                WorldId = world.Id,
                Tick = i + 1,
                SimulationTime = simTime.AddDays(i * 5),
                EventType = i % 2 == 0 ? SimulationEventTypes.AgentHarvestedFood : SimulationEventTypes.AgentDied,
                ActorAgentId = null,
                TargetAgentId = null,
                LocationId = null,
                Data = i % 2 == 0 ? "{\"amount\":1}" : "{\"name\":\"D\",\"cause\":\"Starvation\",\"age\":30}",
                Importance = EventImportance.Normal,
                ImportanceScore = 10,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var timeline = await BuildFoodAutopsyAsync(harness.DbContextFactory, world.Id);

        Assert.Contains(timeline, t => t.EventType == SimulationEventTypes.AgentHarvestedFood);
        Assert.Contains(timeline, t => t.EventType == SimulationEventTypes.AgentDied);
        Assert.True(timeline[0].Tick <= timeline[^1].Tick);
    }

    private static async Task<List<(long Tick, string Type)>> BuildLifeMilestonesAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        World world,
        Guid agentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var events = await db.SimulationEvents.AsNoTracking()
            .Where(e => e.WorldId == world.Id
                && (e.ActorAgentId == agentId || e.TargetAgentId == agentId)
                && e.Importance >= EventImportance.Significant)
            .OrderBy(e => e.Tick)
            .ToListAsync();
        return events.Select(e => (e.Tick, e.EventType)).ToList();
    }

    private static async Task<List<(long Tick, string EventType)>> BuildFoodAutopsyAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        Guid worldId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var events = await db.SimulationEvents.AsNoTracking()
            .Where(e => e.WorldId == worldId)
            .OrderBy(e => e.Tick)
            .ToListAsync();
        return events.Select(e => (e.Tick, e.EventType)).ToList();
    }

    private static async Task<World> SeedWorldAsync(IDbContextFactory<WorldEngineDbContext> factory)
    {
        return await TestSetup.SeedWorldAsync(factory, SimulationStatus.Running, speed: 1.0);
    }

    private static async Task<Agent> SeedAgentAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        Guid worldId,
        double hunger = 0.2,
        double energy = 0.9,
        double food = 0.0,
        double generosity = 0.5)
    {
        var agent = await TestSetup.SeedAgentAsync(factory, worldId, hunger: hunger, energy: energy);
        await using (var db = await factory.CreateDbContextAsync())
        {
            var tracked = await db.Agents.FirstAsync(a => a.Id == agent.Id);
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

    private static async Task<Agent> SeedAgentAtAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        Guid worldId,
        double hunger = 0.2,
        double energy = 0.9,
        double food = 0.0)
    {
        var agent = await TestSetup.SeedAgentAsync(factory, worldId, hunger: hunger, energy: energy);
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

    private static async Task<AgentDecisionContext> BuildDecisionContextAsync(
        TestSetup.Harness harness,
        World world,
        Agent agent,
        IEnumerable<Agent>? nearby = null,
        IEnumerable<AgentRelationship>? relationships = null,
        IEnumerable<AgentMemory>? memories = null)
    {
        await using var db = await harness.DbContextFactory.CreateDbContextAsync();

        var locations = await db.Locations.AsNoTracking().Where(l => l.WorldId == world.Id).ToListAsync();
        var locationsById = locations.ToDictionary(l => l.Id);
        var locationsByName = locations.ToDictionary(l => l.Name);

        var resources = await db.LocationResources.AsNoTracking().ToListAsync();
        var resourceDict = resources.ToDictionary(lr => (lr.LocationId, lr.ResourceType));

        var inventories = await db.AgentInventories.AsNoTracking().ToListAsync();
        var invDict = inventories.ToDictionary(ai => (ai.AgentId, ai.ResourceType));

        var reloaded = await db.Agents.AsNoTracking().FirstAsync(a => a.Id == agent.Id);
        var currentLocation = locationsByName[reloaded.Location];

        var nearbyList = nearby?.ToList() ?? new List<Agent>();
        var relList = relationships?.ToList() ?? new List<AgentRelationship>();
        var memList = memories?.ToList() ?? new List<AgentMemory>();

        var sim = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);

        var actionContext = new WorldEngine.Domain.Actions.ActionContext(
            agent: reloaded,
            world: world,
            simulation: sim,
            currentLocation: currentLocation,
            locationsById: locationsById,
            locationsByName: locationsByName,
            locationResources: resourceDict,
            agentInventories: invDict,
            nearbyAgents: nearbyList,
            newEvents: new List<SimulationEvent>(),
            pendingNewInventories: new List<AgentInventory>(),
            pendingNewLocationResources: new List<LocationResource>(),
            now: DateTime.UtcNow);

        return new AgentDecisionContext(
            agent: reloaded,
            world: world,
            simulation: sim,
            currentLocation: currentLocation,
            locationsById: locationsById,
            locationsByName: locationsByName,
            locationResources: resourceDict,
            agentInventories: invDict,
            nearbyAgents: nearbyList,
            outgoingRelationships: relList,
            recentMemories: memList,
            actionContext: actionContext,
            now: DateTime.UtcNow);
    }
}