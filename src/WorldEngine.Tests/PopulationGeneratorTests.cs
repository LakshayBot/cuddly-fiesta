using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Population;

namespace WorldEngine.Tests;

public class PopulationGeneratorTests
{
    [Fact]
    public void Generate_ReturnsRequestedCount()
    {
        var generator = new PopulationGenerator();
        var world = MakeWorld();
        var random = new SeededRandomSource(42);

        var agents = generator.Generate(world, 50, random, DateTime.UtcNow);

        Assert.Equal(50, agents.Count);
    }

    [Fact]
    public void Generate_AssignsUniqueIds()
    {
        var generator = new PopulationGenerator();
        var world = MakeWorld();
        var random = new SeededRandomSource(7);

        var agents = generator.Generate(world, 50, random, DateTime.UtcNow);

        var ids = agents.Select(a => a.Id).ToHashSet();
        Assert.Equal(50, ids.Count);
    }

    [Fact]
    public void Generate_ProducesRealisticAges()
    {
        var generator = new PopulationGenerator();
        var world = MakeWorld();
        var random = new SeededRandomSource(123);

        var agents = generator.Generate(world, 100, random, DateTime.UtcNow);

        Assert.Contains(agents, a => a.GetAgeYears(world.CurrentSimulationTime) < 18);
        Assert.Contains(agents, a => a.GetAgeYears(world.CurrentSimulationTime) > 50);
        Assert.All(agents, a => Assert.True(a.GetAgeYears(world.CurrentSimulationTime) >= 0));
    }

    [Fact]
    public void Generate_PersonalityTraitsInRange()
    {
        var generator = new PopulationGenerator();
        var world = MakeWorld();
        var random = new SeededRandomSource(99);

        var agents = generator.Generate(world, 50, random, DateTime.UtcNow);

        foreach (var agent in agents)
        {
            Assert.InRange(agent.Curiosity, 0.0, 1.0);
            Assert.InRange(agent.Aggression, 0.0, 1.0);
            Assert.InRange(agent.Empathy, 0.0, 1.0);
            Assert.InRange(agent.Sociability, 0.0, 1.0);
            Assert.InRange(agent.Ambition, 0.0, 1.0);
            Assert.InRange(agent.RiskTolerance, 0.0, 1.0);
            Assert.InRange(agent.Discipline, 0.0, 1.0);
            Assert.InRange(agent.Generosity, 0.0, 1.0);
        }
    }

    [Fact]
    public void Generate_NeedsInExpectedRange()
    {
        var generator = new PopulationGenerator();
        var world = MakeWorld();
        var random = new SeededRandomSource(1);

        var agents = generator.Generate(world, 50, random, DateTime.UtcNow);

        foreach (var agent in agents)
        {
            Assert.InRange(agent.Hunger, 0.0, 1.0);
            Assert.InRange(agent.Energy, 0.0, 1.0);
            Assert.InRange(agent.Health, 0.0, 1.0);
            Assert.InRange(agent.Happiness, 0.0, 1.0);
            Assert.InRange(agent.Safety, 0.0, 1.0);
            Assert.InRange(agent.SocialNeed, 0.0, 1.0);
        }
    }

    [Fact]
    public void Generate_IsDeterministicForSameSeed()
    {
        var generator = new PopulationGenerator();
        var world1 = MakeWorld(seed: 12345);
        var world2 = MakeWorld(seed: 12345);
        var random1 = new SeededRandomSource(12345);
        var random2 = new SeededRandomSource(12345);

        var agents1 = generator.Generate(world1, 10, random1, DateTime.UtcNow);
        var agents2 = generator.Generate(world2, 10, random2, DateTime.UtcNow);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(agents1[i].Name, agents2[i].Name);
            Assert.Equal(agents1[i].Curiosity, agents2[i].Curiosity, 6);
        }
    }

    [Fact]
    public void Generate_Zero_ReturnsEmpty()
    {
        var generator = new PopulationGenerator();
        var world = MakeWorld();
        var random = new SeededRandomSource(1);

        var agents = generator.Generate(world, 0, random, DateTime.UtcNow);

        Assert.Empty(agents);
    }

    private static World MakeWorld(int seed = 42)
    {
        return new World
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            RandomSeed = seed,
            CurrentSimulationTime = new DateTime(2034, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SimulationSpeed = 1.0,
            Status = Domain.Enums.SimulationStatus.Paused,
            TickNumber = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }
}