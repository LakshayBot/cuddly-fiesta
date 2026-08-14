using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldEngine.Api.Contracts;
using WorldEngine.Domain;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Api.Controllers;

[ApiController]
public class InsightsController : ControllerBase
{
    private readonly WorldEngineDbContext _dbContext;

    public InsightsController(WorldEngineDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("api/events/{eventId:guid}")]
    public async Task<ActionResult<EventDetailResponse>> GetEventDetail(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var evt = await _dbContext.SimulationEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        if (evt is null)
        {
            return NotFound();
        }

        var agentIds = new[] { evt.ActorAgentId, evt.TargetAgentId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        var names = await _dbContext.Agents
            .AsNoTracking()
            .Where(a => agentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        var causes = await _dbContext.EventCauses
            .AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.CreatedTick)
            .ToListAsync(cancellationToken);

        var direct = await _dbContext.EventConsequences
            .AsNoTracking()
            .Where(c => c.EventId == eventId && c.Kind == EventConsequenceKinds.Direct)
            .OrderBy(c => c.CreatedTick)
            .ToListAsync(cancellationToken);

        var indirect = await TraceIndirectAsync(eventId, cancellationToken);

        return Ok(new EventDetailResponse(
            evt.Id,
            evt.Tick,
            evt.SimulationTime,
            evt.EventType,
            evt.ActorAgentId,
            evt.ActorAgentId is { } a ? names.GetValueOrDefault(a) : null,
            evt.TargetAgentId,
            evt.TargetAgentId is { } t ? names.GetValueOrDefault(t) : null,
            evt.LocationId,
            evt.Data,
            evt.Importance,
            evt.ImportanceScore,
            causes.Select(c => new EventCauseResponse(
                c.Id, c.CauseType, c.CauseEventId, c.DecisionRecordId, c.Name, c.Value, c.Description, c.CreatedTick)).ToList(),
            direct.Select(c => new EventConsequenceResponse(
                c.Id, c.Kind, c.ConsequenceType, c.ConsequenceEventId, c.ConsequenceMemoryId, c.Description, c.CreatedTick)).ToList(),
            indirect));
    }

    private async Task<List<EventConsequenceResponse>> TraceIndirectAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var eventIds = await _dbContext.SimulationEvents
            .AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (eventIds.Count == 0)
        {
            return new List<EventConsequenceResponse>();
        }

        var causesPointingHere = await _dbContext.EventCauses
            .AsNoTracking()
            .Where(c => c.CauseEventId == eventId)
            .ToListAsync(cancellationToken);

        var result = new List<EventConsequenceResponse>();
        foreach (var cause in causesPointingHere)
        {
            var dependent = await _dbContext.SimulationEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == cause.EventId, cancellationToken);
            if (dependent is null)
            {
                continue;
            }
            result.Add(new EventConsequenceResponse(
                cause.Id,
                EventConsequenceKinds.Indirect,
                EventConsequenceTypes.EventInfluenced,
                dependent.Id,
                null,
                $"{dependent.EventType} (tick {dependent.Tick}) was influenced by this event.",
                cause.CreatedTick));
        }

        return result;
    }

    [HttpGet("api/agents/{agentId:guid}/life")]
    public async Task<ActionResult<AgentLifeResponse>> GetLifeStory(
        Guid agentId,
        CancellationToken cancellationToken)
    {
        var agent = await _dbContext.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);
        if (agent is null)
        {
            return NotFound();
        }

        var world = await _dbContext.Worlds
            .AsNoTracking()
            .FirstAsync(w => w.Id == agent.WorldId, cancellationToken);

        var latestDecision = await _dbContext.AgentDecisionRecords
            .AsNoTracking()
            .Where(d => d.AgentId == agentId)
            .OrderByDescending(d => d.Tick)
            .FirstOrDefaultAsync(cancellationToken);

        var milestoneEvents = await _dbContext.SimulationEvents
            .AsNoTracking()
            .Where(e => e.WorldId == agent.WorldId
                && (e.ActorAgentId == agentId || e.TargetAgentId == agentId)
                && e.Importance >= EventImportance.Significant)
            .OrderBy(e => e.Tick)
            .ToListAsync(cancellationToken);

        var significantMemories = await _dbContext.AgentMemories
            .AsNoTracking()
            .Where(m => m.AgentId == agentId && m.Importance >= 0.5)
            .OrderBy(m => m.CreatedSimulationTime)
            .ToListAsync(cancellationToken);

        var milestones = new List<LifeMilestoneResponse>();

        foreach (var evt in milestoneEvents)
        {
            string summary;
            try
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(evt.Data)
                    ?? new Dictionary<string, object>();
                var isActor = evt.ActorAgentId == agentId;
                summary = evt.EventType switch
                {
                    SimulationEventTypes.AgentBorn => "Born into the world.",
                    SimulationEventTypes.AgentDied => $"Died of {data.GetValueOrDefault("cause")} at age {data.GetValueOrDefault("age")}.",
                    SimulationEventTypes.AgentStole => isActor ? "Stole food from someone." : "Was robbed of food.",
                    SimulationEventTypes.ConflictOccurred => isActor ? "Was involved in a conflict." : "Was the target of a conflict.",
                    SimulationEventTypes.AgentHelped => isActor ? "Helped someone obtain food." : "Was helped with food.",
                    SimulationEventTypes.AgentSharedFood => isActor ? "Shared food with someone." : "Received shared food.",
                    _ => evt.EventType,
                };
            }
            catch
            {
                summary = evt.EventType;
            }

            milestones.Add(new LifeMilestoneResponse(
                evt.Tick,
                evt.SimulationTime,
                evt.EventType,
                evt.Importance,
                summary,
                evt.Id));
        }

        foreach (var mem in significantMemories)
        {
            if (milestones.Any(m => m.EventId == mem.SimulationEventId))
            {
                continue;
            }
            milestones.Add(new LifeMilestoneResponse(
                0,
                mem.CreatedSimulationTime,
                mem.Type,
                EventImportance.Normal,
                mem.Summary,
                mem.SimulationEventId));
        }

        return Ok(new AgentLifeResponse(
            agent.Id,
            agent.Name,
            agent.Alive,
            Math.Round(agent.GetAgeYears(world.CurrentSimulationTime), 2),
            agent.Occupation,
            agent.Location,
            latestDecision?.SelectedActionType,
            latestDecision?.Reasoning,
            milestones.OrderBy(m => m.SimulationTime).ToList()));
    }

    [HttpGet("api/worlds/{worldId:guid}/history")]
    public async Task<ActionResult<IEnumerable<WorldHistoryResponse>>> GetHistory(
        Guid worldId,
        [FromQuery] int? limit,
        [FromQuery] int? minImportance,
        CancellationToken cancellationToken)
    {
        var worldExists = await _dbContext.Worlds
            .AsNoTracking()
            .AnyAsync(w => w.Id == worldId, cancellationToken);
        if (!worldExists)
        {
            return NotFound();
        }

        var query = _dbContext.WorldHistoryEntries
            .AsNoTracking()
            .Where(h => h.WorldId == worldId);

        if (minImportance.HasValue)
        {
            query = query.Where(h => (int)h.Importance >= minImportance.Value);
        }

        var entries = await query
            .OrderByDescending(h => h.SimulationTime)
            .Take(Math.Clamp(limit ?? 100, 1, 500))
            .ToListAsync(cancellationToken);

        return Ok(entries.Select(h => new WorldHistoryResponse(
            h.Id, h.Tick, h.SimulationTime, h.EntryType, h.Importance, h.FactsJson, h.Summary, h.RelatedEventId)));
    }

    [HttpGet("api/worlds/{worldId:guid}/autopsy")]
    public async Task<ActionResult<AutopsyResponse>> GetAutopsy(
        Guid worldId,
        [FromQuery] string? subject,
        CancellationToken cancellationToken)
    {
        var worldExists = await _dbContext.Worlds
            .AsNoTracking()
            .AnyAsync(w => w.Id == worldId, cancellationToken);
        if (!worldExists)
        {
            return NotFound();
        }

        var subjectType = subject ?? "population";
        var timeline = new List<AutopsyFactorResponse>();

        switch (subjectType)
        {
            case "settlement":
                await BuildSettlementAutopsyAsync(worldId, timeline, cancellationToken);
                break;
            case "population":
                await BuildPopulationAutopsyAsync(worldId, timeline, cancellationToken);
                break;
            case "food":
                await BuildFoodAutopsyAsync(worldId, timeline, cancellationToken);
                break;
            default:
                return BadRequest(new { error = "subject must be settlement, population or food." });
        }

        var summary = subjectType switch
        {
            "settlement" => "Settlement decline traced backward through food production, migration, and population events.",
            "population" => "Population change traced backward through deaths, births, and migration events.",
            "food" => "Food supply traced backward through production, scarcity, and settlement events.",
            _ => string.Empty,
        };

        return Ok(new AutopsyResponse(subjectType, summary, timeline));
    }

    private async Task BuildPopulationAutopsyAsync(
        Guid worldId,
        List<AutopsyFactorResponse> timeline,
        CancellationToken cancellationToken)
    {
        var events = await _dbContext.SimulationEvents
            .AsNoTracking()
            .Where(e => e.WorldId == worldId
                && (e.EventType == SimulationEventTypes.AgentDied
                    || e.EventType == SimulationEventTypes.AgentBorn))
            .OrderBy(e => e.SimulationTime)
            .Take(200)
            .ToListAsync(cancellationToken);

        foreach (var evt in events)
        {
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(evt.Data)
                ?? new Dictionary<string, object>();
            var description = evt.EventType == SimulationEventTypes.AgentDied
                ? $"Death: {data.GetValueOrDefault("name")} died of {data.GetValueOrDefault("cause")}."
                : "A new agent was born.";
            timeline.Add(new AutopsyFactorResponse(evt.Tick, evt.SimulationTime, evt.EventType, evt.Id, description));
        }
    }

    private async Task BuildFoodAutopsyAsync(
        Guid worldId,
        List<AutopsyFactorResponse> timeline,
        CancellationToken cancellationToken)
    {
        var foodEvents = await _dbContext.SimulationEvents
            .AsNoTracking()
            .Where(e => e.WorldId == worldId
                && (e.EventType == SimulationEventTypes.AgentHarvestedFood
                    || e.EventType == SimulationEventTypes.AgentAte
                    || e.EventType == SimulationEventTypes.AgentHelped
                    || e.EventType == SimulationEventTypes.AgentStole
                    || e.EventType == SimulationEventTypes.SettlementFormed))
            .OrderBy(e => e.SimulationTime)
            .Take(150)
            .ToListAsync(cancellationToken);

        var counts = foodEvents
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => g.Count());

        timeline.Add(new AutopsyFactorResponse(0, DateTime.MinValue, "Summary",
            null, $"{counts.GetValueOrDefault(SimulationEventTypes.AgentHarvestedFood)} harvests, " +
                  $"{counts.GetValueOrDefault(SimulationEventTypes.AgentAte)} meals, " +
                  $"{counts.GetValueOrDefault(SimulationEventTypes.AgentHelped)} help events, " +
                  $"{counts.GetValueOrDefault(SimulationEventTypes.AgentStole)} thefts observed."));

        foreach (var evt in foodEvents.Take(60))
        {
            var description = evt.EventType switch
            {
                SimulationEventTypes.AgentHarvestedFood => "Food was produced.",
                SimulationEventTypes.AgentAte => "Food was consumed.",
                SimulationEventTypes.AgentHelped => "Food was shared.",
                SimulationEventTypes.AgentStole => "Food was stolen.",
                SimulationEventTypes.SettlementFormed => "A settlement formed near a food source.",
                _ => evt.EventType,
            };
            timeline.Add(new AutopsyFactorResponse(evt.Tick, evt.SimulationTime, evt.EventType, evt.Id, description));
        }
    }

    private async Task BuildSettlementAutopsyAsync(
        Guid worldId,
        List<AutopsyFactorResponse> timeline,
        CancellationToken cancellationToken)
    {
        var settlements = await _dbContext.Settlements
            .AsNoTracking()
            .Where(s => s.WorldId == worldId)
            .ToListAsync(cancellationToken);

        if (settlements.Count == 0)
        {
            timeline.Add(new AutopsyFactorResponse(0, DateTime.MinValue, "None",
                null, "No settlements have formed in this world."));
            return;
        }

        var settlement = settlements.OrderByDescending(s => s.Population).First();

        timeline.Add(new AutopsyFactorResponse(0, settlement.CreationSimulationTime, "SettlementFormed",
            null, $"Settlement {settlement.Name} formed at {settlement.CenterLocationName} with {settlement.Population} residents."));

        var since = settlement.CreationSimulationTime;
        var contributing = await _dbContext.SimulationEvents
            .AsNoTracking()
            .Where(e => e.WorldId == worldId
                && e.SimulationTime >= since
                && (e.EventType == SimulationEventTypes.AgentHarvestedFood
                    || e.EventType == SimulationEventTypes.AgentDied
                    || e.EventType == SimulationEventTypes.AgentMoved
                    || e.EventType == SimulationEventTypes.ConflictOccurred))
            .OrderBy(e => e.SimulationTime)
            .ToListAsync(cancellationToken);

        foreach (var evt in contributing.Take(80))
        {
            var description = evt.EventType switch
            {
                SimulationEventTypes.AgentHarvestedFood => "Food production contributed to viability.",
                SimulationEventTypes.AgentDied => "A resident died, reducing the workforce.",
                SimulationEventTypes.AgentMoved => "An agent moved, changing the population.",
                SimulationEventTypes.ConflictOccurred => "A conflict disrupted the settlement.",
                _ => evt.EventType,
            };
            timeline.Add(new AutopsyFactorResponse(evt.Tick, evt.SimulationTime, evt.EventType, evt.Id, description));
        }
    }
}