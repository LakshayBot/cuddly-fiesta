using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Infrastructure.Simulation;

public sealed class SimulationLoopService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<WorldEngineDbContext> _dbContextFactory;
    private readonly SimulationOptions _options;
    private readonly ILogger<SimulationLoopService> _logger;

    public SimulationLoopService(
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<WorldEngineDbContext> dbContextFactory,
        SimulationOptions options,
        ILogger<SimulationLoopService> logger)
    {
        _scopeFactory = scopeFactory;
        _dbContextFactory = dbContextFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(_options.TickIntervalMs);
        _logger.LogInformation(
            "Simulation loop starting (real tick interval: {IntervalMs}ms, base sim seconds per tick: {BaseSimSeconds}, speed range: {Min}-{Max})",
            _options.TickIntervalMs,
            _options.BaseSimSecondsPerTick,
            _options.MinSimulationSpeed,
            _options.MaxSimulationSpeed);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAllRunningWorldsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Simulation loop iteration failed; will continue");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Simulation loop stopped");
    }

    private async Task TickAllRunningWorldsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<SimulationEngine>();
        var broadcaster = scope.ServiceProvider.GetRequiredService<ISimulationBroadcaster>();

        var worldIds = await engine.ListRunningWorldIdsAsync(cancellationToken);
        if (worldIds.Count == 0)
        {
            return;
        }

        foreach (var worldId in worldIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await engine.TickWorldAsync(worldId, cancellationToken);
            if (!result.Ticked)
            {
                continue;
            }

            var update = new WorldStateUpdate(
                result.WorldId,
                result.TickNumber,
                result.SimulationTime,
                result.Speed,
                result.Status,
                result.UpdatedAt);

            try
            {
                await broadcaster.BroadcastWorldStateAsync(update, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast world state update for {WorldId}", worldId);
            }

            await BroadcastTickEventsAsync(update, broadcaster, cancellationToken);
        }
    }

    private async Task BroadcastTickEventsAsync(
        WorldStateUpdate update,
        ISimulationBroadcaster broadcaster,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var events = await db.SimulationEvents
                .AsNoTracking()
                .Where(e => e.WorldId == update.WorldId && e.Tick == update.TickNumber)
                .OrderBy(e => e.SimulationTime)
                .ToListAsync(cancellationToken);

            if (events.Count == 0)
            {
                return;
            }

            foreach (var evt in events)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Dictionary<string, object?> data;
                try
                {
                    data = System.Text.Json.JsonSerializer
                        .Deserialize<Dictionary<string, object?>>(evt.Data)
                        ?? new Dictionary<string, object?>();
                }
                catch
                {
                    data = new Dictionary<string, object?>();
                }

                var dto = new SimulationEventDto(
                    evt.WorldId,
                    evt.Tick,
                    evt.SimulationTime,
                    evt.EventType,
                    evt.ActorAgentId,
                    evt.TargetAgentId,
                    evt.LocationId,
                    data);

                await broadcaster.BroadcastEventAsync(dto, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast tick events for world {WorldId}", update.WorldId);
        }
    }
}