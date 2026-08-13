using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldEngine.Api.Contracts;
using WorldEngine.Domain.Entities;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Api.Controllers;

[ApiController]
public class AgentsController : ControllerBase
{
    private readonly WorldEngineDbContext _dbContext;

    public AgentsController(WorldEngineDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("api/worlds/{worldId:guid}/agents")]
    public async Task<ActionResult<IEnumerable<AgentResponse>>> ListByWorld(
        Guid worldId,
        [FromQuery] bool? aliveOnly,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var worldExists = await _dbContext.Worlds
            .AsNoTracking()
            .AnyAsync(w => w.Id == worldId, cancellationToken);
        if (!worldExists)
        {
            return NotFound();
        }

        var query = _dbContext.Agents
            .AsNoTracking()
            .Where(a => a.WorldId == worldId);

        if (aliveOnly ?? true)
        {
            query = query.Where(a => a.Alive);
        }

        var projected = await query
            .OrderBy(a => a.Name)
            .Take(Math.Clamp(limit ?? 500, 1, 1000))
            .Select(a => new { a, a.WorldId })
            .Join(
                _dbContext.Worlds.AsNoTracking(),
                x => x.WorldId,
                w => w.Id,
                (x, w) => new { x.a, CurrentSimTime = w.CurrentSimulationTime })
            .ToListAsync(cancellationToken);

        return Ok(projected.Select(x => x.a.ToResponse(x.CurrentSimTime)));
    }

    [HttpGet("api/agents/{agentId:guid}")]
    public async Task<ActionResult<AgentResponse>> GetById(Guid agentId, CancellationToken cancellationToken)
    {
        var projection = await _dbContext.Agents
            .AsNoTracking()
            .Where(a => a.Id == agentId)
            .Join(
                _dbContext.Worlds.AsNoTracking(),
                a => a.WorldId,
                w => w.Id,
                (a, w) => new { Agent = a, CurrentSimTime = w.CurrentSimulationTime })
            .FirstOrDefaultAsync(cancellationToken);

        if (projection is null)
        {
            return NotFound();
        }

        return Ok(projection.Agent.ToResponse(projection.CurrentSimTime));
    }

    [HttpGet("api/agents/{agentId:guid}/relationships")]
    public async Task<ActionResult<IEnumerable<RelationshipResponse>>> GetRelationships(
        Guid agentId,
        [FromQuery] string direction,
        CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Agents.AsNoTracking().AnyAsync(a => a.Id == agentId, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var dir = (direction ?? "outgoing").ToLowerInvariant();

        var outgoing = await _dbContext.AgentRelationships
            .AsNoTracking()
            .Where(r => r.SourceAgentId == agentId)
            .ToListAsync(cancellationToken);

        var incoming = dir == "both" || dir == "incoming"
            ? await _dbContext.AgentRelationships
                .AsNoTracking()
                .Where(r => r.TargetAgentId == agentId)
                .ToListAsync(cancellationToken)
            : new List<AgentRelationship>();

        var targetIds = outgoing.Select(r => r.TargetAgentId)
            .Concat(incoming.Select(r => r.SourceAgentId))
            .Distinct()
            .ToList();

        var names = await _dbContext.Agents
            .AsNoTracking()
            .Where(a => targetIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        var result = new List<RelationshipResponse>();

        if (dir == "both" || dir == "outgoing" || dir == "all")
        {
            foreach (var r in outgoing)
            {
                names.TryGetValue(r.TargetAgentId, out var name);
                result.Add(ToResponse(r.SourceAgentId, r.TargetAgentId, name, r));
            }
        }

        if (dir == "both" || dir == "incoming" || dir == "all")
        {
            foreach (var r in incoming)
            {
                names.TryGetValue(r.SourceAgentId, out var name);
                result.Add(ToResponse(r.SourceAgentId, r.TargetAgentId, name, r));
            }
        }

        return Ok(result);
    }

    [HttpGet("api/agents/{agentId:guid}/memories")]
    public async Task<ActionResult<IEnumerable<MemoryResponse>>> GetMemories(
        Guid agentId,
        [FromQuery] double? minImportance,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Agents.AsNoTracking().AnyAsync(a => a.Id == agentId, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var query = _dbContext.AgentMemories
            .AsNoTracking()
            .Where(m => m.AgentId == agentId);

        if (minImportance.HasValue)
        {
            query = query.Where(m => m.Importance >= minImportance.Value);
        }

        var memories = await query
            .OrderByDescending(m => m.CreatedSimulationTime)
            .Take(Math.Clamp(limit ?? 100, 1, 1000))
            .ToListAsync(cancellationToken);

        return Ok(memories.Select(m => new MemoryResponse(
            m.Id,
            m.AgentId,
            m.SimulationEventId,
            m.Type,
            m.Importance,
            m.EmotionalImpact,
            m.CreatedSimulationTime,
            m.OtherAgentId,
            m.Summary,
            m.CreatedAt)));
    }

    private static RelationshipResponse ToResponse(Guid source, Guid target, string? targetName, AgentRelationship rel) =>
        new(
            source,
            target,
            targetName,
            rel.Trust,
            rel.Affection,
            rel.Respect,
            rel.Fear,
            rel.Anger,
            rel.Familiarity,
            rel.UpdatedAt);

    [HttpGet("api/agents/{agentId:guid}/decisions")]
    public async Task<ActionResult<IEnumerable<DecisionResponse>>> GetDecisions(
        Guid agentId,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Agents.AsNoTracking().AnyAsync(a => a.Id == agentId, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var decisions = await _dbContext.AgentDecisionRecords
            .AsNoTracking()
            .Where(d => d.AgentId == agentId)
            .OrderByDescending(d => d.DecidedAt)
            .Take(Math.Clamp(limit ?? 50, 1, 1000))
            .ToListAsync(cancellationToken);

        return Ok(decisions.Select(d =>
        {
            IReadOnlyList<DecisionActionScore> actions = Array.Empty<DecisionActionScore>();
            try
            {
                var parsed = JsonSerializer.Deserialize<List<DecisionActionScore>>(
                    d.AvailableActionsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                {
                    actions = parsed;
                }
            }
            catch
            {
                actions = Array.Empty<DecisionActionScore>();
            }

            return new DecisionResponse(
                d.Id,
                d.AgentId,
                d.Tick,
                d.SimulationTime,
                d.DecisionSource,
                d.SelectedActionId,
                d.SelectedActionType,
                d.SelectedScore,
                actions,
                d.Reasoning,
                d.DecidedAt,
                d.ModelName,
                d.PromptVersion,
                d.LatencyMs,
                d.FallbackUsed);
        }));
    }
}