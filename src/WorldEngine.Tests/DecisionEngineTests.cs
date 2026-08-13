using Microsoft.EntityFrameworkCore;
using WorldEngine.Domain;
using WorldEngine.Domain.Actions;
using WorldEngine.Domain.AI;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Tests;

public class DecisionEngineTests
{
    [Fact]
    public async Task RuleBased_PrefersEat_WhenHungryWithFood()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var agent = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.95, energy: 0.9, food: 3.0);

        var ctx = await BuildDecisionContextAsync(harness, world, agent);
        var engine = new RuleBasedDecisionEngine(new ActionGenerator());

        var decision = await engine.DecideAsync(ctx, CancellationToken.None);

        Assert.Equal("RuleBased", decision.DecisionSource);
        Assert.False(decision.FallbackUsed);
        Assert.NotNull(decision.SelectedAction);
        Assert.IsType<EatAction>(decision.SelectedAction!.Action);
        Assert.True(decision.AvailableActions.Count > 0);
    }

    [Fact]
    public async Task RuleBased_PrefersRest_WhenEnergyLow()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var agent = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.3, energy: 0.05);

        var ctx = await BuildDecisionContextAsync(harness, world, agent);
        var engine = new RuleBasedDecisionEngine(new ActionGenerator());

        var decision = await engine.DecideAsync(ctx, CancellationToken.None);

        Assert.IsType<RestAction>(decision.SelectedAction!.Action);
    }

    [Fact]
    public async Task RuleBased_FarmerAtFarmHarvests()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var farm = await GetLocationAsync(harness.DbContextFactory, world.Id, LocationTypes.Farm);
        await SetLocationStockAsync(harness.DbContextFactory, farm.Id, ResourceTypes.Food, 10.0);

        var agent = await SeedAgentAtLocationAsync(
            harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Farm,
            hunger: 0.2, energy: 0.9);

        var ctx = await BuildDecisionContextAsync(harness, world, agent);
        var engine = new RuleBasedDecisionEngine(new ActionGenerator());

        var decision = await engine.DecideAsync(ctx, CancellationToken.None);

        Assert.IsType<HarvestFoodAction>(decision.SelectedAction!.Action);
    }

    [Fact]
    public async Task RuleBased_PersonalityBoostsGenerousActions()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var generousAgent = await SeedAgentAsync(
            harness.DbContextFactory, world.Id, hunger: 0.3, energy: 0.9, food: 5.0, generosity: 0.95);
        var starvingTarget = await SeedAgentAtLocationAsync(
            harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village,
            hunger: 0.85, energy: 0.9);

        var ctx = await BuildDecisionContextAsync(
            harness, world, generousAgent, nearby: new List<Agent> { starvingTarget });

        var engine = new RuleBasedDecisionEngine(new ActionGenerator());
        var decision = await engine.DecideAsync(ctx, CancellationToken.None);

        var helpAction = decision.AvailableActions.FirstOrDefault(a => a.ActionType == ActionTypes.Help);
        Assert.NotNull(helpAction);
        Assert.True(helpAction!.Score >= DecisionScoring.HelpBaselineScore);

        var topScoring = decision.AvailableActions.OrderByDescending(a => a.Score).First();
        Assert.True(topScoring.Score > 0);
    }

    [Fact]
    public async Task RuleBased_IdleAlwaysAvailableAsLastResort()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var agent = await SeedAgentAtLocationAsync(
            harness.DbContextFactory, world.Id, Occupations.Unemployed, LocationTypes.River,
            hunger: 0.0, energy: 0.0);

        var ctx = await BuildDecisionContextAsync(harness, world, agent);
        var engine = new RuleBasedDecisionEngine(new ActionGenerator());

        var decision = await engine.DecideAsync(ctx, CancellationToken.None);

        Assert.NotNull(decision.SelectedAction);
        Assert.Contains(decision.AvailableActions, a => a.ActionType == ActionTypes.Idle);
    }

    [Fact]
    public async Task RuleBased_AvailableActionsIncludeAllActionTypes()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var agent = await SeedAgentAtLocationAsync(
            harness.DbContextFactory, world.Id, Occupations.Farmer, LocationTypes.Village,
            hunger: 0.2, energy: 0.9, food: 2.0);

        var target = await SeedAgentAtLocationAsync(
            harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village,
            hunger: 0.5, generosity: 0.7);

        var ctx = await BuildDecisionContextAsync(harness, world, agent, nearby: new List<Agent> { target });

        var engine = new RuleBasedDecisionEngine(new ActionGenerator());
        var decision = await engine.DecideAsync(ctx, CancellationToken.None);

        Assert.Contains(decision.AvailableActions, a => a.ActionType == ActionTypes.Eat);
        Assert.Contains(decision.AvailableActions, a => a.ActionType == ActionTypes.Rest);
        Assert.Contains(decision.AvailableActions, a => a.ActionType == ActionTypes.Talk);
        Assert.Contains(decision.AvailableActions, a => a.ActionType == ActionTypes.Help);
        Assert.Contains(decision.AvailableActions, a => a.ActionType == ActionTypes.Move);
        Assert.Contains(decision.AvailableActions, a => a.ActionType == ActionTypes.Idle);
    }

    [Fact]
    public async Task LLM_FallsBackToRuleBased_WhenSituationNotSignificant()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var agent = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.3, energy: 0.9);

        var llmClient = new FakeLlmClient(_ => "{\"actionId\":\"Eat\"}");
        var fallback = new RuleBasedDecisionEngine(new ActionGenerator());
        var engine = new LLMDecisionEngine(new ActionGenerator(), llmClient, fallback);

        var ctx = await BuildDecisionContextAsync(harness, world, agent);
        var decision = await engine.DecideAsync(ctx, CancellationToken.None);

        Assert.Equal(AgentDecision.Sources.RuleBased, decision.DecisionSource);
        Assert.False(decision.FallbackUsed);
        Assert.Null(decision.ModelName);
    }

    [Fact]
    public async Task LLM_PicksAction_WhenSituationSignificant_AndParsesValidResponse()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var actor = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.3, energy: 0.9, food: 5.0, generosity: 0.7);
        var starvingFriend = await SeedAgentAtLocationAsync(
            harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village,
            hunger: 0.95, energy: 0.9);

        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            db.AgentRelationships.Add(new AgentRelationship
            {
                SourceAgentId = actor.Id,
                TargetAgentId = starvingFriend.Id,
                Trust = 0.6,
                Affection = 0.85,
                Respect = 0.6,
                Fear = 0.0,
                Anger = 0.0,
                Familiarity = 0.3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var llmClient = new FakeLlmClient(_ => $"{{\"actionId\":\"Help:{starvingFriend.Id}\",\"reason\":\"Friend is starving\"}}");
        var fallback = new RuleBasedDecisionEngine(new ActionGenerator());
        var engine = new LLMDecisionEngine(new ActionGenerator(), llmClient, fallback);

        var ctx = await BuildDecisionContextAsync(
            harness, world, actor,
            nearby: new List<Agent> { starvingFriend },
            outgoingRelationships: new List<AgentRelationship>
            {
                new() {
                    SourceAgentId = actor.Id, TargetAgentId = starvingFriend.Id,
                    Trust = 0.6, Affection = 0.85, Respect = 0.6, Fear = 0.0, Anger = 0.0, Familiarity = 0.3,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }
            });

        var decision = await engine.DecideAsync(ctx, CancellationToken.None);

        Assert.StartsWith(AgentDecision.Sources.LlmPrefix, decision.DecisionSource);
        Assert.False(decision.FallbackUsed);
        Assert.Equal(llmClient.ModelName, decision.ModelName);
        Assert.NotNull(decision.SelectedAction);
        Assert.Equal($"Help:{starvingFriend.Id}", decision.SelectedAction!.ActionId);
    }

    [Fact]
    public async Task LLM_FallsBack_WhenResponseInvalid()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var actor = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.3, energy: 0.9, food: 5.0);
        var starvingFriend = await SeedAgentAtLocationAsync(
            harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village,
            hunger: 0.95, energy: 0.9);

        var rel = new AgentRelationship
        {
            SourceAgentId = actor.Id,
            TargetAgentId = starvingFriend.Id,
            Trust = 0.6,
            Affection = 0.85,
            Respect = 0.6,
            Fear = 0.0,
            Anger = 0.0,
            Familiarity = 0.3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            db.AgentRelationships.Add(rel);
            await db.SaveChangesAsync();
        }

        var llmClient = new FakeLlmClient(_ => "this is not valid json");
        var fallback = new RuleBasedDecisionEngine(new ActionGenerator());
        var engine = new LLMDecisionEngine(new ActionGenerator(), llmClient, fallback);

        var ctx = await BuildDecisionContextAsync(
            harness, world, actor,
            nearby: new List<Agent> { starvingFriend },
            outgoingRelationships: new List<AgentRelationship> { rel });

        var decision = await engine.DecideAsync(ctx, CancellationToken.None);

        Assert.Equal(AgentDecision.Sources.RuleBasedFallback, decision.DecisionSource);
        Assert.True(decision.FallbackUsed);
    }

    [Fact]
    public async Task LLM_FallsBack_WhenActionIdInResponseIsNotInAvailableSet()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var actor = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.3, energy: 0.9, food: 5.0);
        var starvingFriend = await SeedAgentAtLocationAsync(
            harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village,
            hunger: 0.95, energy: 0.9);

        var rel = new AgentRelationship
        {
            SourceAgentId = actor.Id,
            TargetAgentId = starvingFriend.Id,
            Trust = 0.6,
            Affection = 0.85,
            Respect = 0.6,
            Fear = 0.0,
            Anger = 0.0,
            Familiarity = 0.3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            db.AgentRelationships.Add(rel);
            await db.SaveChangesAsync();
        }

        var llmClient = new FakeLlmClient(_ => "{\"actionId\":\"Invent:Banana\",\"reason\":\"Hallucinated\"}");
        var fallback = new RuleBasedDecisionEngine(new ActionGenerator());
        var engine = new LLMDecisionEngine(new ActionGenerator(), llmClient, fallback);

        var ctx = await BuildDecisionContextAsync(
            harness, world, actor,
            nearby: new List<Agent> { starvingFriend },
            outgoingRelationships: new List<AgentRelationship> { rel });

        var decision = await engine.DecideAsync(ctx, CancellationToken.None);

        Assert.Equal(AgentDecision.Sources.RuleBasedFallback, decision.DecisionSource);
        Assert.True(decision.FallbackUsed);
    }

    [Fact]
    public async Task LLM_FallsBackWhenClientThrows()
    {
        var harness = TestSetup.CreateHarness();
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var actor = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.3, energy: 0.9, food: 5.0);
        var starvingFriend = await SeedAgentAtLocationAsync(
            harness.DbContextFactory, world.Id, Occupations.Worker, LocationTypes.Village,
            hunger: 0.95, energy: 0.9);

        var rel = new AgentRelationship
        {
            SourceAgentId = actor.Id,
            TargetAgentId = starvingFriend.Id,
            Trust = 0.6,
            Affection = 0.85,
            Respect = 0.6,
            Fear = 0.0,
            Anger = 0.0,
            Familiarity = 0.3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            db.AgentRelationships.Add(rel);
            await db.SaveChangesAsync();
        }

        var llmClient = new ThrowingLlmClient();
        var fallback = new RuleBasedDecisionEngine(new ActionGenerator());
        var engine = new LLMDecisionEngine(new ActionGenerator(), llmClient, fallback);

        var ctx = await BuildDecisionContextAsync(
            harness, world, actor,
            nearby: new List<Agent> { starvingFriend },
            outgoingRelationships: new List<AgentRelationship> { rel });

        var decision = await engine.DecideAsync(ctx, CancellationToken.None);

        Assert.Equal(AgentDecision.Sources.RuleBasedFallback, decision.DecisionSource);
        Assert.True(decision.FallbackUsed);
    }

    [Fact]
    public async Task SimulationNeverCallsLLM_RuleBasedEngineAlone()
    {
        var harness = TestSetup.CreateHarness(withSocialSystem: false);
        var world = await SeedWorldAsync(harness.DbContextFactory);
        var agent = await SeedAgentAsync(harness.DbContextFactory, world.Id, hunger: 0.3, energy: 0.9);

        var ruleBased = new RuleBasedDecisionEngine(new ActionGenerator());

        for (var i = 0; i < 5; i++)
        {
            var sim = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
                world.CurrentSimulationTime.AddSeconds(i * 60), world.CurrentSimulationTime.AddSeconds((i + 1) * 60), i + 1);
            await ruleBased.DecideAsync(
                await BuildDecisionContextAsync(harness, world, agent),
                CancellationToken.None);
        }

        await using var dbVerify = await harness.DbContextFactory.CreateDbContextAsync();
        var decisions = await dbVerify.AgentDecisionRecords.AsNoTracking().CountAsync();
        Assert.Equal(0, decisions);
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
        double generosity = 0.5,
        string occupation = Occupations.Farmer,
        string location = LocationTypes.Village)
    {
        return await SeedAgentAtLocationAsync(
            factory, worldId, occupation, location, hunger, energy, food, generosity);
    }

    private static async Task<Agent> SeedAgentAtLocationAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        Guid worldId,
        string occupation,
        string locationName,
        double hunger = 0.2,
        double energy = 0.9,
        double food = 0.0,
        double generosity = 0.5)
    {
        var agent = await TestSetup.SeedAgentAsync(factory, worldId, hunger: hunger, energy: energy);
        await using (var db = await factory.CreateDbContextAsync())
        {
            var tracked = await db.Agents.FirstAsync(a => a.Id == agent.Id);
            tracked.Occupation = occupation;
            tracked.Location = locationName;
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

    private static async Task<Location> GetLocationAsync(IDbContextFactory<WorldEngineDbContext> factory, Guid worldId, string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Locations.AsNoTracking().FirstAsync(l => l.WorldId == worldId && l.Name == name);
    }

    private static async Task SetLocationStockAsync(IDbContextFactory<WorldEngineDbContext> factory, Guid locationId, string resourceType, double quantity)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.LocationResources.FirstOrDefaultAsync(lr => lr.LocationId == locationId && lr.ResourceType == resourceType);
        if (existing is null)
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
            existing.Quantity = quantity;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    private static async Task<AgentDecisionContext> BuildDecisionContextAsync(
        TestSetup.Harness harness,
        World world,
        Agent agent,
        IEnumerable<Agent>? nearby = null,
        IEnumerable<AgentRelationship>? outgoingRelationships = null,
        IEnumerable<AgentMemory>? recentMemories = null)
    {
        await using var db = await harness.DbContextFactory.CreateDbContextAsync();

        var locations = await db.Locations.AsNoTracking().Where(l => l.WorldId == world.Id).ToListAsync();
        var locationsById = locations.ToDictionary(l => l.Id);
        var locationsByName = locations.ToDictionary(l => l.Name);

        var locationIds = locations.Select(l => l.Id).ToList();
        var resources = await db.LocationResources.AsNoTracking()
            .Where(lr => locationIds.Contains(lr.LocationId)).ToListAsync();
        var resourceDict = resources.ToDictionary(lr => (lr.LocationId, lr.ResourceType));

        var reloadedAgent = await db.Agents.AsNoTracking().FirstAsync(a => a.Id == agent.Id);
        var currentLocation = locationsByName[reloadedAgent.Location];

        var nearbyList = nearby?.ToList() ?? new List<Agent>();
        if (nearby is null)
        {
            var locationAgents = await db.Agents.AsNoTracking()
                .Where(a => a.WorldId == world.Id && a.Location == reloadedAgent.Location && a.Id != reloadedAgent.Id)
                .ToListAsync();
            nearbyList = locationAgents;
        }

        var inventories = await db.AgentInventories.AsNoTracking()
            .Where(ai => ai.AgentId == reloadedAgent.Id).ToListAsync();
        var invDict = inventories.ToDictionary(ai => (ai.AgentId, ai.ResourceType));

        var sim = new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed),
            world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1);

        var actionContext = new ActionContext(
            agent: reloadedAgent,
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
            agent: reloadedAgent,
            world: world,
            simulation: sim,
            currentLocation: currentLocation,
            locationsById: locationsById,
            locationsByName: locationsByName,
            locationResources: resourceDict,
            agentInventories: invDict,
            nearbyAgents: nearbyList,
            outgoingRelationships: outgoingRelationships?.ToList() ?? new List<AgentRelationship>(),
            recentMemories: recentMemories?.ToList() ?? new List<AgentMemory>(),
            actionContext: actionContext,
            now: DateTime.UtcNow);
    }
}

public sealed class FakeLlmClient : ILLMClient
{
    private readonly Func<LlmPromptRequest, string> _responder;

    public FakeLlmClient(Func<LlmPromptRequest, string> responder, string modelName = "fake-llm", int latencyMs = 5)
    {
        _responder = responder;
        ModelName = modelName;
        LatencyMs = latencyMs;
    }

    public string ModelName { get; }

    public int LatencyMs { get; }

    public Task<LlmPromptResponse> CompleteAsync(LlmPromptRequest request, CancellationToken cancellationToken)
    {
        var responseText = _responder(request);
        return Task.FromResult(new LlmPromptResponse(responseText, ModelName, LatencyMs));
    }
}

public sealed class ThrowingLlmClient : ILLMClient
{
    public Task<LlmPromptResponse> CompleteAsync(LlmPromptRequest request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Simulated LLM provider outage");
    }
}