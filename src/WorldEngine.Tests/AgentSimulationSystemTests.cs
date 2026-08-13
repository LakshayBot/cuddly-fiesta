using Microsoft.EntityFrameworkCore;
using WorldEngine.Domain;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Tests;

public class AgentSimulationSystemTests
{
    [Fact]
    public async Task NeedsProgress_NextTickHungerUpEnergyDown()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var agent = await TestSetup.SeedAgentAsync(
            harness.DbContextFactory,
            world.Id,
            hunger: 0.3,
            energy: 0.9,
            socialNeed: 0.3);

        await harness.AgentSystem.ProcessAsync(
            new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed), world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1),
            CancellationToken.None);

        var reloaded = await TestSetup.LoadAgentAsync(harness.DbContextFactory, agent.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(0.3 + NeedRates.HungerPerTick, reloaded!.Hunger, 6);
        Assert.InRange(reloaded.Energy, 0.85, 0.9);
        Assert.Equal(0.3 + NeedRates.SocialNeedPerTick, reloaded.SocialNeed, 6);
    }

    [Fact]
    public async Task NeedsClamp_AtBoundaries()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var agent = await TestSetup.SeedAgentAsync(
            harness.DbContextFactory,
            world.Id,
            hunger: 0.9999,
            energy: 0.0001,
            socialNeed: 0.9999);

        for (var i = 0; i < 50; i++)
        {
            await harness.AgentSystem.ProcessAsync(
                new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed), world.CurrentSimulationTime.AddSeconds(i * 60), world.CurrentSimulationTime.AddSeconds((i + 1) * 60), i + 1),
                CancellationToken.None);
        }

        var reloaded = await TestSetup.LoadAgentAsync(harness.DbContextFactory, agent.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(1.0, reloaded!.Hunger);
        Assert.InRange(reloaded.Energy, 0.0, 1.0);
        Assert.Equal(1.0, reloaded.SocialNeed);
        Assert.InRange(reloaded.Health, 0.0, 1.0);
    }

    [Fact]
    public async Task Starvation_DamagesHealth()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var agent = await TestSetup.SeedAgentAsync(
            harness.DbContextFactory,
            world.Id,
            hunger: 0.9,
            energy: 0.9,
            health: 1.0);

        var previousHealth = (await TestSetup.LoadAgentAsync(harness.DbContextFactory, agent.Id))!.Health;

        for (var i = 0; i < 10; i++)
        {
            await harness.AgentSystem.ProcessAsync(
                new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed), world.CurrentSimulationTime.AddSeconds(i * 60), world.CurrentSimulationTime.AddSeconds((i + 1) * 60), i + 1),
                CancellationToken.None);
        }

        var reloaded = await TestSetup.LoadAgentAsync(harness.DbContextFactory, agent.Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.Health < previousHealth,
            $"Health should decrease under starvation (was {previousHealth}, now {reloaded.Health})");
    }

    [Fact]
    public async Task Starvation_KillsAgentWhenHealthZero()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var agent = await TestSetup.SeedAgentAsync(
            harness.DbContextFactory,
            world.Id,
            hunger: 0.95,
            energy: 0.9,
            health: 0.005);

        var ticksNeeded = 30;
        for (var i = 0; i < ticksNeeded; i++)
        {
            await harness.AgentSystem.ProcessAsync(
                new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed), world.CurrentSimulationTime.AddSeconds(i * 60), world.CurrentSimulationTime.AddSeconds((i + 1) * 60), i + 1),
                CancellationToken.None);

            var check = await TestSetup.LoadAgentAsync(harness.DbContextFactory, agent.Id);
            if (check is { Alive: false })
            {
                break;
            }
        }

        var reloaded = await TestSetup.LoadAgentAsync(harness.DbContextFactory, agent.Id);
        Assert.NotNull(reloaded);
        Assert.False(reloaded!.Alive);
        Assert.Equal("Starvation", reloaded.DeathCause);

        await using var db = await harness.DbContextFactory.CreateDbContextAsync();
        var deathEvent = await db.SimulationEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.WorldId == world.Id && e.EventType == SimulationEventTypes.AgentDied && e.TargetAgentId == agent.Id);
        Assert.NotNull(deathEvent);
        Assert.Equal(reloaded.DeathSimulationTime, deathEvent!.SimulationTime);
    }

    [Fact]
    public async Task DeadAgent_IsNotProcessedNextTick()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var agent = await TestSetup.SeedAgentAsync(
            harness.DbContextFactory,
            world.Id,
            hunger: 1.0,
            energy: 0.0,
            health: 0.0);

        await using (var db = await harness.DbContextFactory.CreateDbContextAsync())
        {
            var tracked = await db.Agents.FirstAsync(a => a.Id == agent.Id);
            tracked.Alive = false;
            tracked.DeathCause = "Test";
            tracked.DeathSimulationTime = world.CurrentSimulationTime;
            await db.SaveChangesAsync();
        }

        var hungerBefore = (await TestSetup.LoadAgentAsync(harness.DbContextFactory, agent.Id))!.Hunger;
        var energyBefore = (await TestSetup.LoadAgentAsync(harness.DbContextFactory, agent.Id))!.Energy;

        await harness.AgentSystem.ProcessAsync(
            new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed), world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1),
            CancellationToken.None);

        var reloaded = await TestSetup.LoadAgentAsync(harness.DbContextFactory, agent.Id);
        Assert.NotNull(reloaded);
        Assert.False(reloaded!.Alive);
        Assert.Equal(hungerBefore, reloaded.Hunger);
        Assert.Equal(energyBefore, reloaded.Energy);
    }

    [Fact]
    public async Task Agent_AgesAsSimulationTimeAdvances()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var birth = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var agent = await TestSetup.SeedAgentAsync(
            harness.DbContextFactory,
            world.Id,
            birthSimulationTime: birth);

        var yearsNow = agent.GetAgeYears(world.CurrentSimulationTime);
        var yearsInOneYear = agent.GetAgeYears(world.CurrentSimulationTime.AddYears(1));
        var yearsInTenYears = agent.GetAgeYears(world.CurrentSimulationTime.AddYears(10));

        Assert.True(yearsNow > 0);
        Assert.InRange(yearsInOneYear - yearsNow, 0.99, 1.01);
        Assert.InRange(yearsInTenYears - yearsNow, 9.99, 10.01);
    }

    [Fact]
    public async Task MaxAge_ForcesDeath()
    {
        var harness = TestSetup.CreateHarness();
        var world = await TestSetup.SeedWorldAsync(
            harness.DbContextFactory,
            SimulationStatus.Running,
            speed: 1.0);

        var ancientBirth = world.CurrentSimulationTime.AddYears(-(NeedRates.MaxAgeYears + 1));
        var agent = await TestSetup.SeedAgentAsync(
            harness.DbContextFactory,
            world.Id,
            birthSimulationTime: ancientBirth,
            hunger: 0.2,
            energy: 0.9,
            health: 1.0);

        await harness.AgentSystem.ProcessAsync(
            new SimulationContext(world, harness.RandomRegistry.GetOrCreate(world.Id, world.RandomSeed), world.CurrentSimulationTime, world.CurrentSimulationTime.AddSeconds(60), 1),
            CancellationToken.None);

        var reloaded = await TestSetup.LoadAgentAsync(harness.DbContextFactory, agent.Id);
        Assert.NotNull(reloaded);
        Assert.False(reloaded!.Alive);
        Assert.Equal("Old age", reloaded.DeathCause);
    }
}