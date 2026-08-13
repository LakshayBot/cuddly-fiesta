using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldEngine.Api.Contracts;
using WorldEngine.Domain.Entities;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Api.Controllers;

[ApiController]
public class SettlementGroupsController : ControllerBase
{
    private readonly WorldEngineDbContext _dbContext;

    public SettlementGroupsController(WorldEngineDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("api/worlds/{worldId:guid}/settlements")]
    public async Task<ActionResult<IEnumerable<SettlementResponse>>> ListSettlements(
        Guid worldId,
        CancellationToken cancellationToken)
    {
        var worldExists = await _dbContext.Worlds
            .AsNoTracking()
            .AnyAsync(w => w.Id == worldId, cancellationToken);
        if (!worldExists)
        {
            return NotFound();
        }

        var settlements = await _dbContext.Settlements
            .AsNoTracking()
            .Where(s => s.WorldId == worldId)
            .OrderBy(s => s.CreationSimulationTime)
            .ToListAsync(cancellationToken);

        return Ok(settlements.Select(ToResponse));
    }

    [HttpGet("api/worlds/{worldId:guid}/groups")]
    public async Task<ActionResult<IEnumerable<GroupResponse>>> ListGroups(
        Guid worldId,
        CancellationToken cancellationToken)
    {
        var worldExists = await _dbContext.Worlds
            .AsNoTracking()
            .AnyAsync(w => w.Id == worldId, cancellationToken);
        if (!worldExists)
        {
            return NotFound();
        }

        var groups = await _dbContext.Groups
            .AsNoTracking()
            .Where(g => g.WorldId == worldId)
            .OrderBy(g => g.FormationSimulationTime)
            .ToListAsync(cancellationToken);

        var groupIds = groups.Select(g => g.Id).ToList();
        var memberships = await _dbContext.GroupMemberships
            .AsNoTracking()
            .Where(gm => groupIds.Contains(gm.GroupId))
            .ToListAsync(cancellationToken);

        var agentIds = memberships.Select(m => m.AgentId).Distinct().ToList();
        var agentNames = await _dbContext.Agents
            .AsNoTracking()
            .Where(a => agentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        var membersByGroup = memberships
            .GroupBy(m => m.GroupId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<GroupMemberResponse>)g.Select(m => new GroupMemberResponse(
                    m.AgentId,
                    agentNames.GetValueOrDefault(m.AgentId) ?? "Unknown",
                    m.Role,
                    m.JoinedAt)).ToList());

        return Ok(groups.Select(g => new GroupResponse(
            g.Id,
            g.WorldId,
            g.Name,
            g.Type,
            g.Status,
            g.FormationReason,
            g.FormationSimulationTime,
            membersByGroup.GetValueOrDefault(g.Id) ?? Array.Empty<GroupMemberResponse>())));
    }

    private static SettlementResponse ToResponse(Settlement s) =>
        new(
            s.Id,
            s.WorldId,
            s.Name,
            s.CenterLocationName,
            s.Population,
            s.Status,
            s.FormationReason,
            s.FirstPopulationAtTick,
            s.CreationSimulationTime,
            s.UpdatedAt);
}