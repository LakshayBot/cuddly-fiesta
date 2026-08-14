using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorldEngine.Domain;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;
using WorldEngine.Infrastructure.Simulation;

namespace WorldEngine.Infrastructure.Simulation.Systems;

public sealed class WorldHistorySystem : ISimulationSystem
{
    private readonly IDbContextFactory<WorldEngineDbContext> _dbContextFactory;
    private readonly SimulationOptions _options;
    private readonly ILogger<WorldHistorySystem> _logger;

    private readonly Dictionary<Guid, WorldTrends> _trends = new();

    public WorldHistorySystem(
        IDbContextFactory<WorldEngineDbContext> dbContextFactory,
        SimulationOptions options,
        ILogger<WorldHistorySystem> logger)
    {
        _dbContextFactory = dbContextFactory;
        _options = options;
        _logger = logger;
    }

    private sealed class WorldTrends
    {
        public int MaxPopulation { get; set; }

        public int CurrentPopulation { get; set; }

        public int DeathsInWindow { get; set; }

        public int FoodCrisisStreak { get; set; }

        public int ConflictsInWindow { get; set; }

        public long LastPopulationDeclineTick { get; set; }

        public long LastFoodCrisisTick { get; set; }

        public long LastMajorConflictTick { get; set; }

        public long LastDeathWaveTick { get; set; }

        public long LastSettlementEntryTick { get; set; }

        public long LastGroupEntryTick { get; set; }

        public bool FirstDeathRecorded { get; set; }
    }

    public async Task ProcessAsync(SimulationContext context, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var worldId = context.World.Id;
        var tick = context.TickNumber;

        if (!_trends.TryGetValue(worldId, out var trends))
        {
            trends = new WorldTrends();
            _trends[worldId] = trends;
        }

        var events = await db.SimulationEvents
            .AsNoTracking()
            .Where(e => e.WorldId == worldId && e.Tick == tick)
            .ToListAsync(cancellationToken);

        var aliveCount = await db.Agents
            .AsNoTracking()
            .CountAsync(a => a.WorldId == worldId && a.Alive, cancellationToken);

        trends.CurrentPopulation = aliveCount;
        if (aliveCount > trends.MaxPopulation)
        {
            trends.MaxPopulation = aliveCount;
        }

        var deaths = events.Count(e => e.EventType == SimulationEventTypes.AgentDied);
        trends.DeathsInWindow += deaths;

        var conflicts = events.Count(e => e.EventType == SimulationEventTypes.ConflictOccurred);
        trends.ConflictsInWindow += conflicts;

        var newEntries = new List<WorldHistoryEntry>();
        var windowTicks = Math.Max(1, _options.HistoryWindowTicks);

        foreach (var evt in events)
        {
            switch (evt.EventType)
            {
                case SimulationEventTypes.SettlementFormed:
                    if (tick - trends.LastSettlementEntryTick >= _options.HistoryEntryCooldownTicks)
                    {
                        var entry = BuildFromJson(evt, WorldHistoryEntryTypes.SettlementFormed, EventImportance.Major,
                            facts => $"A settlement named {facts.GetValueOrDefault("name")} formed at {facts.GetValueOrDefault("location")} " +
                                     $"with {facts.GetValueOrDefault("population")} residents.");
                        if (entry is not null)
                        {
                            newEntries.Add(entry);
                            trends.LastSettlementEntryTick = tick;
                        }
                    }
                    break;

                case SimulationEventTypes.GroupFormed:
                    if (tick - trends.LastGroupEntryTick >= _options.HistoryEntryCooldownTicks)
                    {
                        var entry = BuildFromJson(evt, WorldHistoryEntryTypes.GroupFormed, EventImportance.Normal,
                            facts => $"A {facts.GetValueOrDefault("type")} named {facts.GetValueOrDefault("name")} formed with {facts.GetValueOrDefault("memberCount")} members.");
                        if (entry is not null)
                        {
                            newEntries.Add(entry);
                            trends.LastGroupEntryTick = tick;
                        }
                    }
                    break;

                case SimulationEventTypes.AgentDied:
                    if (!trends.FirstDeathRecorded)
                    {
                        trends.FirstDeathRecorded = true;
                        var first = BuildFromJson(evt, WorldHistoryEntryTypes.FirstDeath, EventImportance.Significant,
                            facts => $"The first recorded death occurred: {facts.GetValueOrDefault("name")} died of {facts.GetValueOrDefault("cause")}.");
                        if (first is not null)
                        {
                            newEntries.Add(first);
                        }
                    }
                    break;
            }
        }

        if (deaths >= _options.DeathWaveThreshold
            && tick - trends.LastDeathWaveTick >= _options.HistoryEntryCooldownTicks)
        {
            newEntries.Add(new WorldHistoryEntry
            {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                Tick = tick,
                SimulationTime = context.NewSimulationTime,
                EntryType = WorldHistoryEntryTypes.DeathWave,
                Importance = EventImportance.Major,
                FactsJson = JsonSerializer.Serialize(new { deaths }),
                Summary = $"{deaths} agents died in a short period.",
                CreatedAt = DateTime.UtcNow,
            });
            trends.LastDeathWaveTick = tick;
            trends.DeathsInWindow = 0;
        }

        if (aliveCount > 0
            && trends.MaxPopulation > 0
            && aliveCount <= trends.MaxPopulation * (1.0 - _options.PopulationDeclineThreshold)
            && tick - trends.LastPopulationDeclineTick >= _options.HistoryEntryCooldownTicks)
        {
            var decline = 1.0 - (double)aliveCount / trends.MaxPopulation;
            newEntries.Add(new WorldHistoryEntry
            {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                Tick = tick,
                SimulationTime = context.NewSimulationTime,
                EntryType = WorldHistoryEntryTypes.PopulationDecline,
                Importance = EventImportance.Historical,
                FactsJson = JsonSerializer.Serialize(new
                {
                    from = trends.MaxPopulation,
                    to = aliveCount,
                    declinePercent = Math.Round(decline * 100, 1),
                }),
                Summary = $"Population declined by {Math.Round(decline * 100, 1)}% (from {trends.MaxPopulation} to {aliveCount}).",
                CreatedAt = DateTime.UtcNow,
            });
            trends.LastPopulationDeclineTick = tick;
            trends.MaxPopulation = aliveCount;
        }

        if (conflicts >= _options.MajorConflictThreshold
            && tick - trends.LastMajorConflictTick >= _options.HistoryEntryCooldownTicks)
        {
            newEntries.Add(new WorldHistoryEntry
            {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                Tick = tick,
                SimulationTime = context.NewSimulationTime,
                EntryType = WorldHistoryEntryTypes.MajorConflict,
                Importance = EventImportance.Major,
                FactsJson = JsonSerializer.Serialize(new { conflicts }),
                Summary = $"{conflicts} conflicts broke out at once.",
                CreatedAt = DateTime.UtcNow,
            });
            trends.LastMajorConflictTick = tick;
            trends.ConflictsInWindow = 0;
        }

        await UpdateFoodCrisisAsync(db, worldId, tick, trends, newEntries, cancellationToken);

        if (newEntries.Count > 0)
        {
            db.WorldHistoryEntries.AddRange(newEntries);
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("World {WorldId} history tick {Tick}: {Count} entries", worldId, tick, newEntries.Count);
        }
    }

    private async Task UpdateFoodCrisisAsync(
        WorldEngineDbContext db,
        Guid worldId,
        long tick,
        WorldTrends trends,
        List<WorldHistoryEntry> newEntries,
        CancellationToken cancellationToken)
    {
        var food = await db.LocationResources
            .AsNoTracking()
            .Where(lr => lr.ResourceType == ResourceTypes.Food)
            .SumAsync(lr => (double)lr.Quantity, cancellationToken);

        if (food < _options.FoodCrisisThreshold)
        {
            trends.FoodCrisisStreak++;
        }
        else
        {
            trends.FoodCrisisStreak = 0;
        }

        if (trends.FoodCrisisStreak >= _options.FoodCrisisWindowTicks
            && tick - trends.LastFoodCrisisTick >= _options.HistoryEntryCooldownTicks)
        {
            newEntries.Add(new WorldHistoryEntry
            {
                Id = Guid.NewGuid(),
                WorldId = worldId,
                Tick = tick,
                SimulationTime = DateTime.UtcNow,
                EntryType = WorldHistoryEntryTypes.FoodCrisis,
                Importance = EventImportance.Historical,
                FactsJson = JsonSerializer.Serialize(new { foodStock = Math.Round(food, 1) }),
                Summary = $"A food shortage began: total food stock fell below {_options.FoodCrisisThreshold:0.#} units.",
                CreatedAt = DateTime.UtcNow,
            });
            trends.LastFoodCrisisTick = tick;
        }
    }

    private static WorldHistoryEntry? BuildFromJson(
        SimulationEvent evt,
        string entryType,
        EventImportance importance,
        Func<Dictionary<string, string>, string> summarize)
    {
        var facts = new Dictionary<string, string>();
        try
        {
            using var doc = JsonDocument.Parse(evt.Data);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                facts[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => prop.Value.GetRawText(),
                };
            }
        }
        catch (JsonException)
        {
        }

        return new WorldHistoryEntry
        {
            Id = Guid.NewGuid(),
            WorldId = evt.WorldId,
            Tick = evt.Tick,
            SimulationTime = evt.SimulationTime,
            EntryType = entryType,
            Importance = importance,
            FactsJson = evt.Data,
            Summary = summarize(facts),
            RelatedEventId = evt.Id,
            CreatedAt = DateTime.UtcNow,
        };
    }
}