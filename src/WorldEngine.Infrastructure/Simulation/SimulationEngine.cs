using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Infrastructure.Simulation;

public sealed class SimulationEngine
{
    private readonly IDbContextFactory<WorldEngineDbContext> _dbContextFactory;
    private readonly IEnumerable<ISimulationSystem> _systems;
    private readonly RandomSourceRegistry _randomRegistry;
    private readonly SimulationOptions _options;
    private readonly ILogger<SimulationEngine> _logger;

    public SimulationEngine(
        IDbContextFactory<WorldEngineDbContext> dbContextFactory,
        IEnumerable<ISimulationSystem> systems,
        RandomSourceRegistry randomRegistry,
        SimulationOptions options,
        ILogger<SimulationEngine> logger)
    {
        _dbContextFactory = dbContextFactory;
        _systems = systems;
        _randomRegistry = randomRegistry;
        _options = options;
        _logger = logger;
    }

    public async Task<TickResult> TickWorldAsync(Guid worldId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var world = await db.Worlds.FirstOrDefaultAsync(w => w.Id == worldId, cancellationToken);
        if (world is null)
        {
            return TickResult.NotFound();
        }

        if (world.Status != SimulationStatus.Running)
        {
            return TickResult.SkippedNotRunning(world.Status);
        }

        var previousTime = world.CurrentSimulationTime;
        var random = _randomRegistry.GetOrCreate(world.Id, world.RandomSeed);

        var advanceSeconds = _options.BaseSimSecondsPerTick * world.SimulationSpeed;
        var newTime = previousTime.AddSeconds(advanceSeconds);
        var newTickNumber = world.TickNumber + 1;

        world.CurrentSimulationTime = newTime;
        world.TickNumber = newTickNumber;
        world.UpdatedAt = DateTime.UtcNow;

        var context = new SimulationContext(world, random, previousTime, newTime, newTickNumber);

        foreach (var system in _systems)
        {
            try
            {
                await system.ProcessAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Simulation system {System} failed for world {WorldId}", system.GetType().Name, worldId);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return TickResult.Success(world, newTickNumber);
    }

    public async Task<IReadOnlyList<Guid>> ListRunningWorldIdsAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Worlds
            .AsNoTracking()
            .Where(w => w.Status == SimulationStatus.Running)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);
    }
}

public sealed record TickResult(
    bool Ticked,
    Guid WorldId,
    long TickNumber,
    DateTime SimulationTime,
    SimulationStatus Status,
    double Speed,
    DateTime UpdatedAt)
{
    public static TickResult NotFound() =>
        new(false, Guid.Empty, 0, DateTime.MinValue, SimulationStatus.Stopped, 0.0, DateTime.MinValue);

    public static TickResult SkippedNotRunning(SimulationStatus status) =>
        new(false, Guid.Empty, 0, DateTime.MinValue, status, 0.0, DateTime.MinValue);

    public static TickResult Success(World world, long tickNumber) =>
        new(true, world.Id, tickNumber, world.CurrentSimulationTime, world.Status, world.SimulationSpeed, world.UpdatedAt);
}