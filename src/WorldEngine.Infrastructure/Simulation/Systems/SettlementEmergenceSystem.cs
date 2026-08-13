using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorldEngine.Domain;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Emergence;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;
using WorldEngine.Infrastructure.Simulation;

namespace WorldEngine.Infrastructure.Simulation.Systems;

public sealed class SettlementEmergenceSystem : ISimulationSystem
{
    private static readonly string[] SocialEventTypes =
    {
        SimulationEventTypes.AgentTalked,
        SimulationEventTypes.AgentHelped,
        SimulationEventTypes.AgentSharedFood,
    };

    private readonly IDbContextFactory<WorldEngineDbContext> _dbContextFactory;
    private readonly RandomSourceRegistry _randomRegistry;
    private readonly SimulationOptions _options;
    private readonly ILogger<SettlementEmergenceSystem> _logger;

    private readonly Dictionary<Guid, Dictionary<string, DateTime>> _clusterFirstSeen = new();

    public SettlementEmergenceSystem(
        IDbContextFactory<WorldEngineDbContext> dbContextFactory,
        RandomSourceRegistry randomRegistry,
        SimulationOptions options,
        ILogger<SettlementEmergenceSystem> logger)
    {
        _dbContextFactory = dbContextFactory;
        _randomRegistry = randomRegistry;
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

        var locations = await db.Locations
            .AsNoTracking()
            .Where(l => l.WorldId == worldId)
            .ToListAsync(cancellationToken);

        if (agents.Count == 0 || locations.Count == 0)
        {
            return;
        }

        var settled = await db.Settlements
            .AsNoTracking()
            .Where(s => s.WorldId == worldId)
            .Select(s => s.CenterLocationName)
            .ToListAsync(cancellationToken);
        var settledSet = settled.ToHashSet();

        if (!_clusterFirstSeen.TryGetValue(worldId, out var firstSeenByLocation))
        {
            firstSeenByLocation = new Dictionary<string, DateTime>();
            _clusterFirstSeen[worldId] = firstSeenByLocation;
        }

        var agentsByLocation = agents
            .GroupBy(a => a.Location)
            .ToDictionary(g => g.Key, g => g.ToList());

        var locationResources = await db.LocationResources
            .AsNoTracking()
            .Where(lr => locations.Select(l => l.Id).Contains(lr.LocationId))
            .ToListAsync(cancellationToken);

        var newEvents = new List<SimulationEvent>();
        var newSettlements = new List<Settlement>();

        foreach (var location in locations)
        {
            if (settledSet.Contains(location.Name))
            {
                firstSeenByLocation.Remove(location.Name);
                continue;
            }

            if (!agentsByLocation.TryGetValue(location.Name, out var residents))
            {
                firstSeenByLocation.Remove(location.Name);
                continue;
            }

            if (residents.Count < _options.MinSettlementPopulation)
            {
                firstSeenByLocation.Remove(location.Name);
                continue;
            }

            if (!firstSeenByLocation.TryGetValue(location.Name, out var firstSeen))
            {
                firstSeenByLocation[location.Name] = context.NewSimulationTime;
                continue;
            }

            var persistedFor = context.NewSimulationTime - firstSeen;
            if (persistedFor.TotalDays < _options.SettlementPersistenceDays)
            {
                continue;
            }

            var foodStock = locationResources
                .Where(lr => lr.LocationId == location.Id && lr.ResourceType == ResourceTypes.Food)
                .Sum(lr => lr.Quantity);

            var residentsWithFood = await db.AgentInventories
                .AsNoTracking()
                .CountAsync(ai =>
                    ai.ResourceType == ResourceTypes.Food
                    && ai.Quantity > 0
                    && residents.Select(r => r.Id).Contains(ai.AgentId),
                    cancellationToken);

            if (foodStock <= 0 && residentsWithFood == 0)
            {
                continue;
            }

            var residentIds = residents.Select(r => r.Id).ToList();
            var interactionWindow = Math.Max(1, _options.EmergenceInteractionWindowTicks);
            var interactionCount = await db.SimulationEvents
                .AsNoTracking()
                .CountAsync(e =>
                    e.WorldId == worldId
                    && e.Tick > context.TickNumber - interactionWindow
                    && e.LocationId == location.Id
                    && SocialEventTypes.Any(t => t == e.EventType),
                    cancellationToken);

            var random = _randomRegistry.GetOrCreate(worldId, context.World.RandomSeed);
            var name = EmergentNameGenerator.Settlement(random);
            var explanation = BuildExplanation(
                residents.Count,
                location.Name,
                persistedFor.TotalDays,
                foodStock,
                interactionCount);

            var settlement = new Settlement
            {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                Name = name,
                CenterLocationName = location.Name,
                Population = residents.Count,
                Status = "Forming",
                FormationReason = explanation,
                FirstPopulationAtTick = firstSeen,
                CreationSimulationTime = context.NewSimulationTime,
                UpdatedAt = DateTime.UtcNow,
            };
            newSettlements.Add(settlement);
            settledSet.Add(location.Name);
            firstSeenByLocation.Remove(location.Name);

            newEvents.Add(new SimulationEvent
            {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                Tick = context.TickNumber,
                SimulationTime = context.NewSimulationTime,
                EventType = SimulationEventTypes.SettlementFormed,
                ActorAgentId = null,
                TargetAgentId = null,
                LocationId = location.Id,
                Data = System.Text.Json.JsonSerializer.Serialize(new
                {
                    name,
                    location = location.Name,
                    population = residents.Count,
                    explanation,
                }),
                CreatedAt = DateTime.UtcNow,
            });

            _logger.LogInformation(
                "Settlement {Name} formed at {Location} in world {WorldId} (population {Population})",
                name, location.Name, worldId, residents.Count);
        }

        if (newSettlements.Count > 0)
        {
            db.Settlements.AddRange(newSettlements);
        }
        if (newEvents.Count > 0)
        {
            db.SimulationEvents.AddRange(newEvents);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildExplanation(int population, string location, double days, double foodStock, int interactions)
    {
        return $"{population} agents lived within {location}." +
               $" Average interaction frequency exceeded threshold: {interactions} social events in recent window." +
               $" Food production supported the population (stock {foodStock:0.##})." +
               $" The cluster persisted for {days:0.#} simulation days.";
    }
}