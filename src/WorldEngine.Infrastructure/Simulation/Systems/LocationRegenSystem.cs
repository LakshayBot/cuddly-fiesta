using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorldEngine.Domain;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Infrastructure.Simulation.Systems;

public sealed class LocationRegenSystem : ISimulationSystem
{
    private readonly IDbContextFactory<WorldEngineDbContext> _dbContextFactory;
    private readonly SimulationOptions _options;
    private readonly ILogger<LocationRegenSystem> _logger;

    public LocationRegenSystem(
        IDbContextFactory<WorldEngineDbContext> dbContextFactory,
        SimulationOptions options,
        ILogger<LocationRegenSystem> logger)
    {
        _dbContextFactory = dbContextFactory;
        _options = options;
        _logger = logger;
    }

    public async Task ProcessAsync(SimulationContext context, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var locations = await db.Locations
            .Where(l => l.WorldId == context.World.Id)
            .ToListAsync(cancellationToken);

        if (locations.Count == 0)
        {
            return;
        }

        var locationIds = locations.Select(l => l.Id).ToList();

        var existingResources = await db.LocationResources
            .Where(lr => locationIds.Contains(lr.LocationId))
            .ToListAsync(cancellationToken);

        var byKey = existingResources.ToDictionary(lr => (lr.LocationId, lr.ResourceType));
        var now = DateTime.UtcNow;

        foreach (var location in locations)
        {
            foreach (var (resource, regen, capacity) in RegenRulesFor(location.Type))
            {
                ApplyRegenSingle(location.Id, resource, regen, capacity, byKey, now);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private IEnumerable<(string Resource, double Regen, double Capacity)> RegenRulesFor(string locationType)
    {
        switch (locationType)
        {
            case LocationTypes.Village:
                yield return (ResourceTypes.Food, _options.VillageFoodRegenPerTick, _options.VillageFoodCapacity);
                yield return (ResourceTypes.Wood, _options.VillageWoodRegenPerTick, _options.VillageWoodCapacity);
                yield return (ResourceTypes.Water, _options.VillageWaterRegenPerTick, _options.VillageWaterCapacity);
                break;
            case LocationTypes.Farm:
                yield return (ResourceTypes.Food, _options.FarmFoodRegenPerTick, _options.FarmFoodCapacity);
                break;
            case LocationTypes.Forest:
                yield return (ResourceTypes.Wood, _options.ForestWoodRegenPerTick, _options.ForestWoodCapacity);
                break;
            case LocationTypes.River:
                yield return (ResourceTypes.Water, _options.RiverWaterRegenPerTick, _options.RiverWaterCapacity);
                break;
        }
    }

    private static void ApplyRegenSingle(
        Guid locationId,
        string resourceType,
        double regenPerTick,
        double capacity,
        Dictionary<(Guid, string), LocationResource> byKey,
        DateTime now)
    {
        var key = (locationId, resourceType);
        if (!byKey.TryGetValue(key, out var resource))
        {
            return;
        }

        resource.Quantity = Math.Min(capacity, resource.Quantity + regenPerTick);
        resource.UpdatedAt = now;
    }
}