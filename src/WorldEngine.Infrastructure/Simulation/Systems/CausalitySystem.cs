using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorldEngine.Domain;
using WorldEngine.Domain.AI;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;
using WorldEngine.Infrastructure.Simulation;

namespace WorldEngine.Infrastructure.Simulation.Systems;

public sealed class CausalitySystem : ISimulationSystem
{
    private static readonly string[] ActionEventTypes =
    {
        SimulationEventTypes.AgentAte,
        SimulationEventTypes.AgentRested,
        SimulationEventTypes.AgentMoved,
        SimulationEventTypes.AgentHarvestedFood,
        SimulationEventTypes.AgentGatheredWood,
        SimulationEventTypes.AgentWorked,
        SimulationEventTypes.AgentTalked,
        SimulationEventTypes.AgentHelped,
        SimulationEventTypes.AgentSharedFood,
        SimulationEventTypes.AgentTraded,
        SimulationEventTypes.AgentStole,
        SimulationEventTypes.AgentInsulted,
    };

    private static readonly string[] SocialEventTypes =
    {
        SimulationEventTypes.AgentHelped,
        SimulationEventTypes.AgentSharedFood,
        SimulationEventTypes.AgentTalked,
        SimulationEventTypes.AgentStole,
        SimulationEventTypes.AgentInsulted,
    };

    private readonly IDbContextFactory<WorldEngineDbContext> _dbContextFactory;
    private readonly SimulationOptions _options;
    private readonly ILogger<CausalitySystem> _logger;

    public CausalitySystem(
        IDbContextFactory<WorldEngineDbContext> dbContextFactory,
        SimulationOptions options,
        ILogger<CausalitySystem> logger)
    {
        _dbContextFactory = dbContextFactory;
        _options = options;
        _logger = logger;
    }

    public async Task ProcessAsync(SimulationContext context, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var worldId = context.World.Id;
        var tick = context.TickNumber;

        var events = await db.SimulationEvents
            .AsNoTracking()
            .Where(e => e.WorldId == worldId && e.Tick == tick)
            .OrderBy(e => e.SimulationTime)
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            await MaybeEscalateAsync(db, worldId, tick, cancellationToken);
            return;
        }

        var actorIds = events
            .Where(e => e.ActorAgentId.HasValue)
            .Select(e => e.ActorAgentId!.Value)
            .Distinct()
            .ToList();

        var decisions = await db.AgentDecisionRecords
            .AsNoTracking()
            .Where(d => d.WorldId == worldId && d.Tick == tick && actorIds.Contains(d.AgentId))
            .ToListAsync(cancellationToken);
        var decisionByAgent = decisions
            .GroupBy(d => d.AgentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.DecidedAt).First());

        var agentNames = await db.Agents
            .AsNoTracking()
            .Where(a => actorIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        var memories = await db.AgentMemories
            .AsNoTracking()
            .Where(m => m.CreatedSimulationTime >= context.PreviousSimulationTime)
            .ToListAsync(cancellationToken);
        var memoriesByEvent = memories
            .GroupBy(m => m.SimulationEventId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var recentStolenIds = await db.SimulationEvents
            .AsNoTracking()
            .Where(e => e.WorldId == worldId
                && e.EventType == SimulationEventTypes.AgentStole
                && e.Tick > tick - _options.CauseLookbackTicks)
            .ToListAsync(cancellationToken);
        var stolenByTarget = recentStolenIds
            .GroupBy(e => e.TargetAgentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Tick).ToList());

        var newCauses = new List<EventCause>();
        var newConsequences = new List<EventConsequence>();

        foreach (var evt in events)
        {
            var causes = BuildCauses(evt, decisionByAgent, agentNames, stolenByTarget);
            newCauses.AddRange(causes);

            var consequences = BuildConsequences(evt, agentNames, memoriesByEvent, stolenByTarget);
            newConsequences.AddRange(consequences);
        }

        if (newCauses.Count > 0)
        {
            db.EventCauses.AddRange(newCauses);
        }
        if (newConsequences.Count > 0)
        {
            db.EventConsequences.AddRange(newConsequences);
        }

        await AssignImportanceAsync(db, events, worldId, tick, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await MaybeEscalateAsync(db, worldId, tick, cancellationToken);

        if (newCauses.Count > 0 || newConsequences.Count > 0)
        {
            _logger.LogDebug(
                "Causality for world {WorldId} tick {Tick}: {Causes} causes, {Consequences} consequences",
                worldId, tick, newCauses.Count, newConsequences.Count);
        }
    }

    private static List<EventCause> BuildCauses(
        SimulationEvent evt,
        IReadOnlyDictionary<Guid, AgentDecisionRecord> decisionByAgent,
        IReadOnlyDictionary<Guid, string> agentNames,
        IReadOnlyDictionary<Guid, List<SimulationEvent>> stolenByTarget)
    {
        var causes = new List<EventCause>();

        if (ActionEventTypes.Contains(evt.EventType) && evt.ActorAgentId.HasValue
            && decisionByAgent.TryGetValue(evt.ActorAgentId.Value, out var decision))
        {
            causes.Add(new EventCause
            {
                Id = Guid.NewGuid(),
                EventId = evt.Id,
                CauseType = EventCauseTypes.Decision,
                DecisionRecordId = decision.Id,
                Name = "AgentDecision",
                Value = decision.SelectedScore,
                Description = $"The agent selected {decision.SelectedActionType} (score {decision.SelectedScore:0.##}).",
                CreatedTick = evt.Tick,
            });

            if (!string.IsNullOrWhiteSpace(decision.SelectedFactorsJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(decision.SelectedFactorsJson);
                    foreach (var f in doc.RootElement.EnumerateArray())
                    {
                        var type = f.GetProperty("type").GetString();
                        var name = f.GetProperty("name").GetString();
                        var value = f.TryGetProperty("value", out var v) ? v.GetDouble() : 0;
                        var desc = f.TryGetProperty("description", out var d) ? d.GetString() : null;
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(desc))
                        {
                            continue;
                        }
                        causes.Add(new EventCause
                        {
                            Id = Guid.NewGuid(),
                            EventId = evt.Id,
                            CauseType = MapFactorType(type),
                            Name = name,
                            Value = value,
                            Description = desc,
                            CreatedTick = evt.Tick,
                        });
                    }
                }
                catch (JsonException)
                {
                }
            }
        }

        switch (evt.EventType)
        {
            case SimulationEventTypes.AgentDied:
                AddStateCause(causes, evt, "DeathHealth", "Health reached a fatal threshold.");
                if (stolenByTarget.TryGetValue(evt.TargetAgentId ?? Guid.Empty, out var stolenFrom))
                {
                    foreach (var theft in stolenFrom.Take(3))
                    {
                        causes.Add(new EventCause
                        {
                            Id = Guid.NewGuid(),
                            EventId = evt.Id,
                            CauseType = EventCauseTypes.Event,
                            CauseEventId = theft.Id,
                            Name = "PreviousTheft",
                            Description = "The agent had recently been robbed of food.",
                            CreatedTick = evt.Tick,
                        });
                    }
                }
                break;

            case SimulationEventTypes.AgentStole:
                AddStateCause(causes, evt, "HungerCritical", "Hunger was critical at the time of the theft.");
                AddStateCause(causes, evt, "InventoryEmpty", "The agent had no food of their own.");
                break;
        }

        return causes;
    }

    private static List<EventConsequence> BuildConsequences(
        SimulationEvent evt,
        IReadOnlyDictionary<Guid, string> agentNames,
        IReadOnlyDictionary<Guid, List<AgentMemory>> memoriesByEvent,
        IReadOnlyDictionary<Guid, List<SimulationEvent>> stolenByTarget)
    {
        var consequences = new List<EventConsequence>();

        if (SocialEventTypes.Contains(evt.EventType))
        {
            var otherName = evt.TargetAgentId.HasValue
                ? agentNames.GetValueOrDefault(evt.TargetAgentId.Value) ?? "#" + evt.TargetAgentId.Value.ToString()[..4]
                : null;
            var relDesc = evt.EventType switch
            {
                SimulationEventTypes.AgentHelped or SimulationEventTypes.AgentSharedFood =>
                    $"Relationship with {otherName ?? "the other agent"} became more positive (trust/affection increased).",
                SimulationEventTypes.AgentTalked =>
                    $"Familiarity with {otherName ?? "the other agent"} increased.",
                SimulationEventTypes.AgentStole =>
                    $"Relationship with {otherName ?? "the victim"} deteriorated (trust decreased, anger increased).",
                _ => $"Relationship with {otherName ?? "the other agent"} changed.",
            };
            consequences.Add(new EventConsequence
            {
                Id = Guid.NewGuid(),
                EventId = evt.Id,
                Kind = EventConsequenceKinds.Direct,
                ConsequenceType = EventConsequenceTypes.RelationshipChanged,
                Description = relDesc,
                CreatedTick = evt.Tick,
            });
        }

        if (memoriesByEvent.TryGetValue(evt.Id, out var mems))
        {
            foreach (var mem in mems.Take(4))
            {
                consequences.Add(new EventConsequence
                {
                    Id = Guid.NewGuid(),
                    EventId = evt.Id,
                    Kind = EventConsequenceKinds.Direct,
                    ConsequenceType = EventConsequenceTypes.MemoryCreated,
                    ConsequenceMemoryId = mem.Id,
                    Description = $"{agentNames.GetValueOrDefault(mem.AgentId) ?? "An agent"} formed a memory: {mem.Summary}",
                    CreatedTick = evt.Tick,
                });
            }
        }

        if (evt.EventType == SimulationEventTypes.AgentDied && evt.TargetAgentId.HasValue)
        {
            consequences.Add(new EventConsequence
            {
                Id = Guid.NewGuid(),
                EventId = evt.Id,
                Kind = EventConsequenceKinds.Direct,
                ConsequenceType = EventConsequenceTypes.StateChanged,
                Description = "The agent's possessions were redistributed and dependents lost a provider.",
                CreatedTick = evt.Tick,
            });

            foreach (var theft in stolenByTarget.GetValueOrDefault(evt.TargetAgentId.Value, new List<SimulationEvent>()).Take(2))
            {
                consequences.Add(new EventConsequence
                {
                    Id = Guid.NewGuid(),
                    EventId = theft.Id,
                    Kind = EventConsequenceKinds.Direct,
                    ConsequenceType = EventConsequenceTypes.EventInfluenced,
                    ConsequenceEventId = evt.Id,
                    Description = "This theft contributed to the victim's later death.",
                    CreatedTick = evt.Tick,
                });
            }
        }

        return consequences;
    }

    private static void AddStateCause(List<EventCause> causes, SimulationEvent evt, string name, string description)
    {
        causes.Add(new EventCause
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            CauseType = EventCauseTypes.State,
            Name = name,
            Description = description,
            CreatedTick = evt.Tick,
        });
    }

    private static string MapFactorType(string? factorType) => factorType switch
    {
        "Need" => EventCauseTypes.State,
        "Relationship" => EventCauseTypes.Relationship,
        "Personality" => EventCauseTypes.State,
        "State" => EventCauseTypes.State,
        "Resource" => EventCauseTypes.Resource,
        _ => EventCauseTypes.State,
    };

    private async Task AssignImportanceAsync(
        WorldEngineDbContext db,
        IReadOnlyList<SimulationEvent> events,
        Guid worldId,
        long tick,
        CancellationToken cancellationToken)
    {
        var tracked = new Dictionary<Guid, SimulationEvent>();
        foreach (var evt in events)
        {
            var affected = 1;
            if (evt.ActorAgentId.HasValue) affected++;
            if (evt.TargetAgentId.HasValue) affected++;

            var magnitude = evt.EventType switch
            {
                SimulationEventTypes.AgentDied => 1.0,
                SimulationEventTypes.AgentStole => 0.7,
                SimulationEventTypes.ConflictOccurred => 0.9,
                SimulationEventTypes.SettlementFormed => 1.0,
                _ => 0.3,
            };
            var relationshipImpact = SocialEventTypes.Contains(evt.EventType) ? 0.7 : 0.0;
            var populationImpact = evt.EventType == SimulationEventTypes.AgentDied ? 0.5 : 0.0;

            var (importance, score) = EventImportanceEvaluator.Evaluate(
                evt.EventType, affected, magnitude, relationshipImpact, 0.0, populationImpact);

            var stored = tracked.TryGetValue(evt.Id, out var e)
                ? e
                : await db.SimulationEvents.FirstOrDefaultAsync(x => x.Id == evt.Id, cancellationToken);
            if (stored is null)
            {
                continue;
            }
            tracked[evt.Id] = stored;
            stored.Importance = importance;
            stored.ImportanceScore = score;
        }
    }

    private async Task MaybeEscalateAsync(
        WorldEngineDbContext db,
        Guid worldId,
        long tick,
        CancellationToken cancellationToken)
    {
        if (_options.ImportanceEscalationIntervalTicks <= 0
            || tick % _options.ImportanceEscalationIntervalTicks != 0)
        {
            return;
        }

        var candidates = await db.SimulationEvents
            .AsNoTracking()
            .Where(e => e.WorldId == worldId && e.Importance >= EventImportance.Significant)
            .OrderByDescending(e => e.SimulationTime)
            .Take(500)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        var ids = candidates.Select(c => c.Id).ToList();
        var consequenceCounts = await db.EventConsequences
            .AsNoTracking()
            .Where(c => ids.Contains(c.EventId))
            .GroupBy(c => c.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count, cancellationToken);

        foreach (var evt in candidates)
        {
            var boost = consequenceCounts.GetValueOrDefault(evt.Id, 0) * 4;
            if (boost <= 0)
            {
                continue;
            }
            var stored = await db.SimulationEvents.FirstOrDefaultAsync(e => e.Id == evt.Id, cancellationToken);
            if (stored is null)
            {
                continue;
            }
            stored.ImportanceScore += boost;
            if (stored.ImportanceScore >= 60 && stored.Importance < EventImportance.Historical)
            {
                stored.Importance = EventImportance.Historical;
            }
            else if (stored.ImportanceScore >= 40 && stored.Importance < EventImportance.Major)
            {
                stored.Importance = EventImportance.Major;
            }
        }
    }
}