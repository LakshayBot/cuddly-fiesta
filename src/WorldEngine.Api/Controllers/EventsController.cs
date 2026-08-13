using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldEngine.Api.Contracts;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Api.Controllers;

[ApiController]
public class EventsController : ControllerBase
{
    private readonly WorldEngineDbContext _dbContext;

    public EventsController(WorldEngineDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("api/worlds/{worldId:guid}/events")]
    public async Task<ActionResult<IEnumerable<SimulationEventResponse>>> ListByWorld(
        Guid worldId,
        [FromQuery] string? eventType,
        [FromQuery] int? limit,
        [FromQuery] long? sinceTick,
        [FromQuery] long? beforeTick,
        CancellationToken cancellationToken)
    {
        var worldExists = await _dbContext.Worlds
            .AsNoTracking()
            .AnyAsync(w => w.Id == worldId, cancellationToken);
        if (!worldExists)
        {
            return NotFound();
        }

        var query = _dbContext.SimulationEvents
            .AsNoTracking()
            .Where(e => e.WorldId == worldId);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        if (sinceTick.HasValue)
        {
            query = query.Where(e => e.Tick >= sinceTick.Value);
        }

        if (beforeTick.HasValue)
        {
            query = query.Where(e => e.Tick < beforeTick.Value);
        }

        var events = await query
            .OrderByDescending(e => e.SimulationTime)
            .ThenByDescending(e => e.Tick)
            .Take(Math.Clamp(limit ?? 100, 1, 1000))
            .ToListAsync(cancellationToken);

        return Ok(events.Select(e => e.ToResponse()));
    }
}