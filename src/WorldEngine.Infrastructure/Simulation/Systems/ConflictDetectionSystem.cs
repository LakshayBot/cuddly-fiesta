using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorldEngine.Domain;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;
using WorldEngine.Infrastructure.Simulation;

namespace WorldEngine.Infrastructure.Simulation.Systems;

public sealed class ConflictDetectionSystem : ISimulationSystem
{
    private readonly IDbContextFactory<WorldEngineDbContext> _dbContextFactory;
    private readonly SimulationOptions _options;
    private readonly ILogger<ConflictDetectionSystem> _logger;

    private readonly Dictionary<Guid, Dictionary<(Guid, Guid), long>> _lastConflictTick = new();

    public ConflictDetectionSystem(
        IDbContextFactory<WorldEngineDbContext> dbContextFactory,
        SimulationOptions options,
        ILogger<ConflictDetectionSystem> logger)
    {
        _dbContextFactory = dbContextFactory;
        _options = options;
        _logger = logger;
    }

    public async Task ProcessAsync(SimulationContext context, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var worldId = context.World.Id;

        var agents = await db.Agents
            .AsNoTracking()
            .Where(a => a.WorldId == worldId && a.Alive)
            .ToListAsync(cancellationToken);

        if (agents.Count == 0)
        {
            return;
        }

        var agentIds = agents.Select(a => a.Id).ToList();
        var relationships = await db.AgentRelationships
            .AsNoTracking()
            .Where(r => agentIds.Contains(r.SourceAgentId))
            .ToListAsync(cancellationToken);

        var locations = await db.Locations
            .AsNoTracking()
            .Where(l => l.WorldId == worldId)
            .ToListAsync(cancellationToken);

        var locationIds = locations.Select(l => l.Id).ToList();
        var resources = await db.LocationResources
            .AsNoTracking()
            .Where(lr => locationIds.Contains(lr.LocationId))
            .ToListAsync(cancellationToken);

        var locationCapacity = new Dictionary<Guid, double>();
        foreach (var location in locations)
        {
            var cap = location.Type switch
            {
                LocationTypes.Farm => _options.FarmFoodCapacity,
                LocationTypes.Forest => _options.ForestWoodCapacity,
                LocationTypes.River => _options.RiverWaterCapacity,
                _ => 100.0,
            };
            locationCapacity[location.Id] = cap;
        }

        var agentsById = agents.ToDictionary(a => a.Id);
        var locationByName = locations.ToDictionary(l => l.Name);

        if (!_lastConflictTick.TryGetValue(worldId, out var lastTicks))
        {
            lastTicks = new Dictionary<(Guid, Guid), long>();
            _lastConflictTick[worldId] = lastTicks;
        }

        var newEvents = new List<SimulationEvent>();
        var now = DateTime.UtcNow;

        foreach (var rel in relationships)
        {
            if (rel.Anger < _options.ConflictAngerThreshold) continue;

            if (!agentsById.TryGetValue(rel.SourceAgentId, out var source)) continue;
            if (!agentsById.TryGetValue(rel.TargetAgentId, out var target)) continue;

            if (source.Location != target.Location) continue;
            if (!locationByName.TryGetValue(source.Location, out var location)) continue;

            var key = (rel.SourceAgentId, rel.TargetAgentId);
            if (lastTicks.TryGetValue(key, out var lastTick)
                && context.TickNumber - lastTick < _options.ConflictCooldownTicks)
            {
                continue;
            }

            var causes = new List<string>();
            var foodStock = resources
                .Where(lr => lr.LocationId == location.Id && lr.ResourceType == ResourceTypes.Food)
                .Sum(lr => lr.Quantity);
            var capacity = locationCapacity.GetValueOrDefault(location.Id, 100.0);
            var scarcity = capacity > 0 ? foodStock / capacity : 0.0;

            if (scarcity < _options.ScarcityThreshold)
            {
                causes.Add("ResourceScarcity");
            }

            if (target.Hunger >= _options.ConflictHungerThreshold)
            {
                causes.Add("Hunger");
            }

            if (causes.Count == 0)
            {
                causes.Add("NegativeRelationship");
            }

            lastTicks[key] = context.TickNumber;

            newEvents.Add(new SimulationEvent
            {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                Tick = context.TickNumber,
                SimulationTime = context.NewSimulationTime,
                EventType = SimulationEventTypes.ConflictOccurred,
                ActorAgentId = source.Id,
                TargetAgentId = target.Id,
                LocationId = location.Id,
                Data = System.Text.Json.JsonSerializer.Serialize(new
                {
                    causes,
                    anger = Math.Round(rel.Anger, 3),
                    foodStock = Math.Round(foodStock, 2),
                    scarcity = Math.Round(scarcity, 3),
                    targetHunger = Math.Round(target.Hunger, 3),
                }),
                CreatedAt = now,
            });

            _logger.LogInformation(
                "Conflict detected: {Source} -> {Target} ({Causes}) at {Location} in world {WorldId}",
                source.Name, target.Name, string.Join(", ", causes), location.Name, worldId);
        }

        if (newEvents.Count > 0)
        {
            db.SimulationEvents.AddRange(newEvents);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}