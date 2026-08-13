using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using WorldEngine.Domain;
using WorldEngine.Domain.AI;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;
using WorldEngine.Infrastructure.Simulation;
using WorldEngine.Infrastructure.Simulation.Systems;

namespace WorldEngine.Tests;

internal static class TestSetup
{
    public sealed class TestDbContextFactory : IDbContextFactory<WorldEngineDbContext>
    {
        private readonly DbContextOptions<WorldEngineDbContext> _options;
        private readonly InMemoryDatabaseRoot _root;

        public TestDbContextFactory(DbContextOptions<WorldEngineDbContext> options, InMemoryDatabaseRoot root)
        {
            _options = options;
            _root = root;
        }

        public WorldEngineDbContext CreateDbContext() => new(_options);
    }

    public sealed record Harness(
        IDbContextFactory<WorldEngineDbContext> DbContextFactory,
        RandomSourceRegistry RandomRegistry,
        SimulationEngine Engine,
        AgentSimulationSystem AgentSystem,
        SocialConsequenceSystem SocialSystem,
        SettlementEmergenceSystem SettlementSystem,
        GroupEmergenceSystem GroupSystem,
        ConflictDetectionSystem ConflictSystem,
        SimulationOptions Options);

    public static Harness CreateHarness(
        int tickIntervalMs = 100,
        double baseSimSecondsPerTick = 60.0,
        double minSpeed = 0.0,
        double maxSpeed = 1000.0,
        string? databaseName = null,
        bool withSocialSystem = true,
        SimulationOptions? options = null)
    {
        var root = new InMemoryDatabaseRoot();
        var optionsBuilder = new DbContextOptionsBuilder<WorldEngineDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString(), root)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning));
        var factory = new TestDbContextFactory(optionsBuilder.Options, root);

        var simOptions = options ?? new SimulationOptions
        {
            TickIntervalMs = tickIntervalMs,
            BaseSimSecondsPerTick = baseSimSecondsPerTick,
            MinSimulationSpeed = minSpeed,
            MaxSimulationSpeed = maxSpeed,
        };

        var registry = new RandomSourceRegistry();
        var decisionEngine = new RuleBasedDecisionEngine(new ActionGenerator());
        var agentSystem = new AgentSimulationSystem(
            factory,
            decisionEngine,
            NullLogger<AgentSimulationSystem>.Instance);
        var socialSystem = new SocialConsequenceSystem(
            factory,
            NullLogger<SocialConsequenceSystem>.Instance);
        var settlementSystem = new SettlementEmergenceSystem(
            factory,
            registry,
            simOptions,
            NullLogger<SettlementEmergenceSystem>.Instance);
        var groupSystem = new GroupEmergenceSystem(
            factory,
            registry,
            simOptions,
            NullLogger<GroupEmergenceSystem>.Instance);
        var conflictSystem = new ConflictDetectionSystem(
            factory,
            simOptions,
            NullLogger<ConflictDetectionSystem>.Instance);

        var systems = new List<ISimulationSystem> { agentSystem };
        if (withSocialSystem)
        {
            systems.Add(socialSystem);
        }

        var engine = new SimulationEngine(
            factory,
            systems,
            registry,
            simOptions,
            NullLogger<SimulationEngine>.Instance);

        return new Harness(
            factory, registry, engine, agentSystem, socialSystem,
            settlementSystem, groupSystem, conflictSystem, simOptions);
    }

    public static Harness CreateHarnessWithoutSystems(
        int tickIntervalMs = 100,
        double baseSimSecondsPerTick = 60.0,
        double minSpeed = 0.0,
        double maxSpeed = 1000.0,
        string? databaseName = null)
    {
        var root = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<WorldEngineDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString(), root)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;
        var factory = new TestDbContextFactory(options, root);

        var simOptions = new SimulationOptions
        {
            TickIntervalMs = tickIntervalMs,
            BaseSimSecondsPerTick = baseSimSecondsPerTick,
            MinSimulationSpeed = minSpeed,
            MaxSimulationSpeed = maxSpeed,
        };

        var registry = new RandomSourceRegistry();
        var engine = new SimulationEngine(
            factory,
            Array.Empty<ISimulationSystem>(),
            registry,
            simOptions,
            NullLogger<SimulationEngine>.Instance);

        return new Harness(factory, registry, engine, null!, null!, null!, null!, null!, simOptions);
    }

    public static async Task<World> SeedWorldAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        SimulationStatus status,
        double speed,
        DateTime? currentSimTime = null,
        int seed = 42,
        bool withLocations = true)
    {
        await using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var world = new World
        {
            Id = Guid.NewGuid(),
            Name = $"World-{Guid.NewGuid():N}",
            RandomSeed = seed,
            CurrentSimulationTime = currentSimTime ?? now,
            SimulationSpeed = speed,
            Status = status,
            TickNumber = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Worlds.Add(world);

        if (withLocations)
        {
            foreach (var locType in LocationTypes.All)
            {
                var loc = new Location
                {
                    Id = Guid.NewGuid(),
                    WorldId = world.Id,
                    Name = locType,
                    Type = locType,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.Locations.Add(loc);
            }
        }

        await db.SaveChangesAsync();
        return world;
    }

    public static async Task<Agent> SeedAgentAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        Guid worldId,
        DateTime? birthSimulationTime = null,
        bool alive = true,
        double hunger = 0.2,
        double energy = 0.9,
        double health = 1.0,
        double happiness = 0.7,
        double safety = 0.95,
        double socialNeed = 0.3)
    {
        await using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            WorldId = worldId,
            Name = $"Agent-{Guid.NewGuid():N}",
            BirthSimulationTime = birthSimulationTime ?? now.AddYears(-30),
            Alive = alive,
            Location = "Village",
            Occupation = "Farmer",
            Money = 10m,
            Hunger = hunger,
            Energy = energy,
            Health = health,
            Happiness = happiness,
            Safety = safety,
            SocialNeed = socialNeed,
            Curiosity = 0.5,
            Aggression = 0.5,
            Empathy = 0.5,
            Sociability = 0.5,
            Ambition = 0.5,
            RiskTolerance = 0.5,
            Discipline = 0.5,
            Generosity = 0.5,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();
        return agent;
    }

    public static async Task<Agent?> LoadAgentAsync(
        IDbContextFactory<WorldEngineDbContext> factory,
        Guid agentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Agents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentId);
    }
}