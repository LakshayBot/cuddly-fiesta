using Microsoft.EntityFrameworkCore;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Tests;

public class WorldDbContextTests
{
    [Fact]
    public void CanCreateAndRetrieveWorld()
    {
        var options = new DbContextOptionsBuilder<WorldEngineDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var now = DateTime.UtcNow;

        using (var ctx = new WorldEngineDbContext(options))
        {
            var world = new World
            {
                Id = Guid.NewGuid(),
                Name = "Test World",
                RandomSeed = 42,
                CurrentSimulationTime = now,
                SimulationSpeed = 1.0,
                Status = SimulationStatus.Paused,
                TickNumber = 0,
                CreatedAt = now,
                UpdatedAt = now,
            };
            ctx.Worlds.Add(world);
            ctx.SaveChanges();
        }

        using (var ctx = new WorldEngineDbContext(options))
        {
            var world = ctx.Worlds.Single();
            Assert.Equal("Test World", world.Name);
            Assert.Equal(42, world.RandomSeed);
            Assert.Equal(SimulationStatus.Paused, world.Status);
            Assert.Equal(0, world.TickNumber);
        }
    }
}