using Microsoft.EntityFrameworkCore;
using WorldEngine.Domain.Enums;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Tests;

public class SimulationEngineTests
{
    [Fact]
    public async Task PausedWorld_DoesNotAdvance()
    {
        var harness = TestSetup.CreateHarness(baseSimSecondsPerTick: 60);
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            status: SimulationStatus.Paused,
            speed: 1.0);

        var result = await harness.Engine.TickWorldAsync(world.Id, CancellationToken.None);

        Assert.False(result.Ticked);

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var reloaded = await db.Worlds.AsNoTracking().FirstAsync(w => w.Id == world.Id);
        Assert.Equal(0, reloaded.TickNumber);
        Assert.Equal(world.CurrentSimulationTime, reloaded.CurrentSimulationTime);
        Assert.Equal(SimulationStatus.Paused, reloaded.Status);
    }

    [Fact]
    public async Task StoppedWorld_DoesNotAdvance()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            status: SimulationStatus.Stopped,
            speed: 1.0);

        var result = await harness.Engine.TickWorldAsync(world.Id, CancellationToken.None);

        Assert.False(result.Ticked);

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var reloaded = await db.Worlds.AsNoTracking().FirstAsync(w => w.Id == world.Id);
        Assert.Equal(0, reloaded.TickNumber);
        Assert.Equal(SimulationStatus.Stopped, reloaded.Status);
    }

    [Fact]
    public async Task RunningWorld_AdvancesTickAndTime()
    {
        var harness = TestSetup.CreateHarness(baseSimSecondsPerTick: 60);
        var startTime = new DateTime(2034, 5, 12, 0, 0, 0, DateTimeKind.Utc);
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            status: SimulationStatus.Running,
            speed: 1.0,
            currentSimTime: startTime);

        var result = await harness.Engine.TickWorldAsync(world.Id, CancellationToken.None);

        Assert.True(result.Ticked);
        Assert.Equal(1, result.TickNumber);
        Assert.Equal(startTime.AddSeconds(60), result.SimulationTime);

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var reloaded = await db.Worlds.AsNoTracking().FirstAsync(w => w.Id == world.Id);
        Assert.Equal(1, reloaded.TickNumber);
        Assert.Equal(startTime.AddSeconds(60), reloaded.CurrentSimulationTime);
        Assert.Equal(SimulationStatus.Running, reloaded.Status);
    }

    [Fact]
    public async Task RepeatedTicks_IncrementTickNumberMonotonically()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            status: SimulationStatus.Running,
            speed: 1.0);

        for (var i = 0; i < 5; i++)
        {
            var result = await harness.Engine.TickWorldAsync(world.Id, CancellationToken.None);
            Assert.True(result.Ticked);
            Assert.Equal(i + 1, result.TickNumber);
        }

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var reloaded = await db.Worlds.AsNoTracking().FirstAsync(w => w.Id == world.Id);
        Assert.Equal(5, reloaded.TickNumber);
    }

    [Fact]
    public async Task HigherSpeed_AdvancesMoreTimePerTick()
    {
        var harness = TestSetup.CreateHarness(baseSimSecondsPerTick: 60);
        var startTime = new DateTime(2034, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var slow = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            status: SimulationStatus.Running,
            speed: 1.0,
            currentSimTime: startTime);

        var fast = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            status: SimulationStatus.Running,
            speed: 10.0,
            currentSimTime: startTime);

        var slowResult = await harness.Engine.TickWorldAsync(slow.Id, CancellationToken.None);
        var fastResult = await harness.Engine.TickWorldAsync(fast.Id, CancellationToken.None);

        Assert.True(slowResult.Ticked);
        Assert.True(fastResult.Ticked);

        var slowAdvance = slowResult.SimulationTime - startTime;
        var fastAdvance = fastResult.SimulationTime - startTime;

        Assert.Equal(TimeSpan.FromSeconds(60), slowAdvance);
        Assert.Equal(TimeSpan.FromSeconds(600), fastAdvance);
        Assert.Equal(10.0, fastAdvance.TotalSeconds / slowAdvance.TotalSeconds);
    }

    [Fact]
    public async Task ZeroSpeed_DoesNotAdvanceTime()
    {
        var harness = TestSetup.CreateHarness(baseSimSecondsPerTick: 60);
        var startTime = new DateTime(2034, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            status: SimulationStatus.Running,
            speed: 0.0,
            currentSimTime: startTime);

        var result = await harness.Engine.TickWorldAsync(world.Id, CancellationToken.None);

        Assert.True(result.Ticked);
        Assert.Equal(1, result.TickNumber);
        Assert.Equal(startTime, result.SimulationTime);
    }

    [Fact]
    public async Task State_PersistsAcrossNewEngineInstance()
    {
        var harness = TestSetup.CreateHarness(baseSimSecondsPerTick: 60);
        var startTime = new DateTime(2034, 7, 4, 12, 0, 0, DateTimeKind.Utc);
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            status: SimulationStatus.Running,
            speed: 2.0,
            currentSimTime: startTime);

        await harness.Engine.TickWorldAsync(world.Id, CancellationToken.None);
        await harness.Engine.TickWorldAsync(world.Id, CancellationToken.None);
        await harness.Engine.TickWorldAsync(world.Id, CancellationToken.None);

        var freshEngine = new WorldEngine.Infrastructure.Simulation.SimulationEngine(
            harness.DbContextFactory,
            Array.Empty<WorldEngine.Domain.Simulation.ISimulationSystem>(),
            new WorldEngine.Infrastructure.Simulation.RandomSourceRegistry(),
            harness.Options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WorldEngine.Infrastructure.Simulation.SimulationEngine>.Instance);

        var result = await freshEngine.TickWorldAsync(world.Id, CancellationToken.None);

        Assert.True(result.Ticked);
        Assert.Equal(4, result.TickNumber);
        Assert.Equal(startTime.AddSeconds(60 * 2.0 * 4), result.SimulationTime);
    }

    [Fact]
    public async Task Engine_UsesSeededRandomSourceFromWorldSeed()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            status: SimulationStatus.Running,
            speed: 1.0,
            seed: 12345);

        var result = await harness.Engine.TickWorldAsync(world.Id, CancellationToken.None);

        Assert.True(result.Ticked);

        var source = harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed);
        Assert.NotNull(source);
    }

    [Fact]
    public async Task UnknownWorld_ReturnsNotFoundResult()
    {
        var harness = TestSetup.CreateHarness();
        var result = await harness.Engine.TickWorldAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Ticked);
        Assert.Equal(Guid.Empty, result.WorldId);
    }
}