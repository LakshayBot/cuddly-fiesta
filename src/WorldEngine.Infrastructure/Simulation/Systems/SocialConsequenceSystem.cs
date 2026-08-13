using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorldEngine.Domain;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Infrastructure.Simulation.Systems;

public sealed class SocialConsequenceSystem : ISimulationSystem
{
    private readonly IDbContextFactory<WorldEngineDbContext> _dbContextFactory;
    private readonly ILogger<SocialConsequenceSystem> _logger;

    public SocialConsequenceSystem(
        IDbContextFactory<WorldEngineDbContext> dbContextFactory,
        ILogger<SocialConsequenceSystem> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task ProcessAsync(SimulationContext context, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var events = await db.SimulationEvents
            .AsNoTracking()
            .Where(e => e.WorldId == context.World.Id && e.Tick == context.TickNumber)
            .OrderBy(e => e.SimulationTime)
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return;
        }

        var participatingAgents = events
            .Where(e => e.ActorAgentId.HasValue || e.TargetAgentId.HasValue)
            .SelectMany(e => new[] { e.ActorAgentId, e.TargetAgentId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (participatingAgents.Count == 0)
        {
            return;
        }

        var existingRelationships = await db.AgentRelationships
            .Where(r => participatingAgents.Contains(r.SourceAgentId))
            .ToListAsync(cancellationToken);

        var relByKey = existingRelationships.ToDictionary(r => (r.SourceAgentId, r.TargetAgentId));
        var agentNames = await db.Agents
            .AsNoTracking()
            .Where(a => participatingAgents.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        var newMemories = new List<AgentMemory>();
        var now = DateTime.UtcNow;

        foreach (var evt in events)
        {
            switch (evt.EventType)
            {
                case SimulationEventTypes.AgentHelped:
                case SimulationEventTypes.AgentSharedFood:
                    ApplyPositiveInteraction(evt, relByKey, agentNames, newMemories, context, now);
                    break;

                case SimulationEventTypes.AgentTalked:
                    ApplyTalk(evt, relByKey, agentNames, newMemories, context, now);
                    break;

                case SimulationEventTypes.AgentStole:
                case SimulationEventTypes.AgentInsulted:
                    ApplyNegativeInteraction(evt, relByKey, agentNames, newMemories, now);
                    break;
            }
        }

        foreach (var rel in relByKey.Values)
        {
            rel.UpdatedAt = now;
        }

        var newRelationships = relByKey.Values
            .Where(r => existingRelationships.All(e => e.SourceAgentId != r.SourceAgentId || e.TargetAgentId != r.TargetAgentId))
            .ToList();

        if (newRelationships.Count > 0)
        {
            db.AgentRelationships.AddRange(newRelationships);
        }

        if (newMemories.Count > 0)
        {
            db.AgentMemories.AddRange(newMemories);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyPositiveInteraction(
        SimulationEvent evt,
        Dictionary<(Guid Source, Guid Target), AgentRelationship> relByKey,
        Dictionary<Guid, string> agentNames,
        List<AgentMemory> newMemories,
        SimulationContext context,
        DateTime now)
    {
        if (!evt.ActorAgentId.HasValue || !evt.TargetAgentId.HasValue)
        {
            return;
        }

        var actor = evt.ActorAgentId.Value;
        var target = evt.TargetAgentId.Value;
        agentNames.TryGetValue(actor, out var actorName);
        agentNames.TryGetValue(target, out var targetName);

        var trustDelta = evt.EventType == SimulationEventTypes.AgentSharedFood
            ? RelationshipDeltas.ShareFoodTrust
            : RelationshipDeltas.HelpTrust;
        var affectionDelta = evt.EventType == SimulationEventTypes.AgentSharedFood
            ? RelationshipDeltas.ShareFoodAffection
            : RelationshipDeltas.HelpAffection;
        var respectDelta = evt.EventType == SimulationEventTypes.AgentSharedFood
            ? RelationshipDeltas.ShareFoodRespect
            : RelationshipDeltas.HelpRespect;
        var familiarityDelta = evt.EventType == SimulationEventTypes.AgentSharedFood
            ? RelationshipDeltas.ShareFoodFamiliarity
            : RelationshipDeltas.HelpFamiliarity;

        UpdateRelationship(relByKey, target, actor, trustDelta, affectionDelta, respectDelta, 0.0, 0.0, familiarityDelta, now);

        var memoryImportance = evt.EventType == SimulationEventTypes.AgentSharedFood
            ? MemoryImportance.Significant
            : MemoryImportance.Notable;
        var memoryType = evt.EventType == SimulationEventTypes.AgentSharedFood
            ? MemoryTypes.ReceivedFood
            : MemoryTypes.ReceivedHelp;

        newMemories.Add(new AgentMemory
        {
            Id = Guid.NewGuid(),
            AgentId = target,
            SimulationEventId = evt.Id,
            Type = memoryType,
            Importance = memoryImportance,
            EmotionalImpact = affectionDelta * 5.0,
            CreatedSimulationTime = evt.SimulationTime,
            OtherAgentId = actor,
            Summary = $"{actorName ?? "Someone"} helped me when I needed it.",
            CreatedAt = now,
        });

        newMemories.Add(new AgentMemory
        {
            Id = Guid.NewGuid(),
            AgentId = actor,
            SimulationEventId = evt.Id,
            Type = evt.EventType == SimulationEventTypes.AgentSharedFood ? MemoryTypes.GaveFood : MemoryTypes.HelpedSomeone,
            Importance = memoryImportance * 0.7,
            EmotionalImpact = affectionDelta * 2.0,
            CreatedSimulationTime = evt.SimulationTime,
            OtherAgentId = target,
            Summary = $"I helped {targetName ?? "someone"}.",
            CreatedAt = now,
        });
    }

    private static void ApplyTalk(
        SimulationEvent evt,
        Dictionary<(Guid Source, Guid Target), AgentRelationship> relByKey,
        Dictionary<Guid, string> agentNames,
        List<AgentMemory> newMemories,
        SimulationContext context,
        DateTime now)
    {
        if (!evt.ActorAgentId.HasValue || !evt.TargetAgentId.HasValue)
        {
            return;
        }

        var actor = evt.ActorAgentId.Value;
        var target = evt.TargetAgentId.Value;
        agentNames.TryGetValue(actor, out var actorName);
        agentNames.TryGetValue(target, out var targetName);

        UpdateRelationship(relByKey, target, actor, 0.0, RelationshipDeltas.TalkAffection, 0.0, 0.0, 0.0, RelationshipDeltas.TalkFamiliarity, now);

        newMemories.Add(new AgentMemory
        {
            Id = Guid.NewGuid(),
            AgentId = target,
            SimulationEventId = evt.Id,
            Type = MemoryTypes.Talked,
            Importance = MemoryImportance.Minor,
            EmotionalImpact = 0.05,
            CreatedSimulationTime = evt.SimulationTime,
            OtherAgentId = actor,
            Summary = $"{actorName ?? "Someone"} spoke with me.",
            CreatedAt = now,
        });
    }

    private static void ApplyNegativeInteraction(
        SimulationEvent evt,
        Dictionary<(Guid Source, Guid Target), AgentRelationship> relByKey,
        Dictionary<Guid, string> agentNames,
        List<AgentMemory> newMemories,
        DateTime now)
    {
        if (!evt.ActorAgentId.HasValue || !evt.TargetAgentId.HasValue)
        {
            return;
        }

        var actor = evt.ActorAgentId.Value;
        var target = evt.TargetAgentId.Value;
        agentNames.TryGetValue(actor, out var actorName);

        double trustDelta;
        double affectionDelta;
        double respectDelta;
        double fearDelta;
        double angerDelta;
        double familiarityDelta;
        string memoryType;
        double memoryImportance;
        string verb;

        if (evt.EventType == SimulationEventTypes.AgentStole)
        {
            trustDelta = RelationshipDeltas.StealTrust;
            angerDelta = RelationshipDeltas.StealAnger;
            respectDelta = RelationshipDeltas.StealRespect;
            fearDelta = 0.05;
            affectionDelta = -0.05;
            familiarityDelta = RelationshipDeltas.StealFamiliarity;
            memoryType = MemoryTypes.WitnessedDeath;
            memoryImportance = MemoryImportance.Significant;
            verb = "stole from";
        }
        else
        {
            trustDelta = -0.05;
            angerDelta = RelationshipDeltas.InsultAnger;
            respectDelta = RelationshipDeltas.InsultRespect;
            fearDelta = 0.0;
            affectionDelta = -0.04;
            familiarityDelta = 0.01;
            memoryType = "WasInsulted";
            memoryImportance = MemoryImportance.Notable;
            verb = "insulted";
        }

        UpdateRelationship(relByKey, target, actor, trustDelta, affectionDelta, respectDelta, fearDelta, angerDelta, familiarityDelta, now);

        newMemories.Add(new AgentMemory
        {
            Id = Guid.NewGuid(),
            AgentId = target,
            SimulationEventId = evt.Id,
            Type = memoryType,
            Importance = memoryImportance,
            EmotionalImpact = angerDelta - trustDelta,
            CreatedSimulationTime = evt.SimulationTime,
            OtherAgentId = actor,
            Summary = $"{actorName ?? "Someone"} {verb} me.",
            CreatedAt = now,
        });
    }

    private static void UpdateRelationship(
        Dictionary<(Guid Source, Guid Target), AgentRelationship> relByKey,
        Guid source,
        Guid target,
        double trustDelta,
        double affectionDelta,
        double respectDelta,
        double fearDelta,
        double angerDelta,
        double familiarityDelta,
        DateTime now)
    {
        var key = (source, target);
        if (!relByKey.TryGetValue(key, out var rel))
        {
            rel = new AgentRelationship
            {
                SourceAgentId = source,
                TargetAgentId = target,
                Trust = RelationshipDefaults.Trust,
                Affection = RelationshipDefaults.Affection,
                Respect = RelationshipDefaults.Respect,
                Fear = RelationshipDefaults.Fear,
                Anger = RelationshipDefaults.Anger,
                Familiarity = RelationshipDefaults.Familiarity,
                CreatedAt = now,
                UpdatedAt = now,
            };
            relByKey[key] = rel;
        }

        rel.Trust = Clamp01(rel.Trust + trustDelta);
        rel.Affection = Clamp01(rel.Affection + affectionDelta);
        rel.Respect = Clamp01(rel.Respect + respectDelta);
        rel.Fear = Clamp01(rel.Fear + fearDelta);
        rel.Anger = Clamp01(rel.Anger + angerDelta);
        rel.Familiarity = Clamp01(rel.Familiarity + familiarityDelta);
        rel.UpdatedAt = now;
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}