using WorldEngine.Domain;
using WorldEngine.Domain.Entities;
using WorldEngine.Infrastructure.Simulation;

namespace WorldEngine.Infrastructure.Worlds;

public sealed class WorldSeeder
{
    private readonly SimulationOptions _options;
    private readonly DateTime _now;

    public WorldSeeder(SimulationOptions options, DateTime now)
    {
        _options = options;
        _now = now;
    }

    public IReadOnlyList<Location> SeedLocations(World world)
    {
        var village = new Location
        {
            Id = Guid.NewGuid(),
            WorldId = world.Id,
            Name = LocationTypes.Village,
            Type = LocationTypes.Village,
            CreatedAt = _now,
            UpdatedAt = _now,
        };
        var farm = new Location
        {
            Id = Guid.NewGuid(),
            WorldId = world.Id,
            Name = LocationTypes.Farm,
            Type = LocationTypes.Farm,
            CreatedAt = _now,
            UpdatedAt = _now,
        };
        var forest = new Location
        {
            Id = Guid.NewGuid(),
            WorldId = world.Id,
            Name = LocationTypes.Forest,
            Type = LocationTypes.Forest,
            CreatedAt = _now,
            UpdatedAt = _now,
        };
        var river = new Location
        {
            Id = Guid.NewGuid(),
            WorldId = world.Id,
            Name = LocationTypes.River,
            Type = LocationTypes.River,
            CreatedAt = _now,
            UpdatedAt = _now,
        };

        return new[] { village, farm, forest, river };
    }

    public IReadOnlyList<LocationResource> SeedLocationResources(IReadOnlyList<Location> locations)
    {
        var resources = new List<LocationResource>();

        foreach (var location in locations)
        {
            foreach (var (type, amount) in InitialStocksFor(location.Type))
            {
                resources.Add(new LocationResource
                {
                    LocationId = location.Id,
                    ResourceType = type,
                    Quantity = amount,
                    UpdatedAt = _now,
                });
            }
        }

        return resources;
    }

    private IEnumerable<(string Type, double Amount)> InitialStocksFor(string locationType)
    {
        switch (locationType)
        {
            case LocationTypes.Village:
                yield return (ResourceTypes.Food, _options.VillageFoodSeed);
                yield return (ResourceTypes.Wood, _options.VillageWoodSeed);
                yield return (ResourceTypes.Water, _options.VillageWaterSeed);
                break;
            case LocationTypes.Farm:
                yield return (ResourceTypes.Food, _options.FarmFoodSeed);
                break;
            case LocationTypes.Forest:
                yield return (ResourceTypes.Wood, _options.ForestWoodSeed);
                break;
            case LocationTypes.River:
                yield return (ResourceTypes.Water, _options.RiverWaterSeed);
                break;
        }
    }
}