using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldEngine.Api.Contracts;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Api.Controllers;

[ApiController]
public class LocationsController : ControllerBase
{
    private readonly WorldEngineDbContext _dbContext;

    public LocationsController(WorldEngineDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("api/worlds/{worldId:guid}/locations")]
    public async Task<ActionResult<IEnumerable<LocationResponse>>> ListByWorld(
        Guid worldId,
        CancellationToken cancellationToken)
    {
        var locations = await _dbContext.Locations
            .AsNoTracking()
            .Where(l => l.WorldId == worldId)
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);

        if (locations.Count == 0)
        {
            var worldExists = await _dbContext.Worlds
                .AsNoTracking()
                .AnyAsync(w => w.Id == worldId, cancellationToken);
            if (!worldExists)
            {
                return NotFound();
            }
            return Ok(Array.Empty<LocationResponse>());
        }

        var locationIds = locations.Select(l => l.Id).ToList();
        var resources = await _dbContext.LocationResources
            .AsNoTracking()
            .Where(lr => locationIds.Contains(lr.LocationId))
            .ToListAsync(cancellationToken);

        var byLocation = resources
            .GroupBy(r => r.LocationId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, double>)g.ToDictionary(r => r.ResourceType, r => r.Quantity));

        var result = locations.Select(loc =>
        {
            byLocation.TryGetValue(loc.Id, out var res);
            return loc.ToResponse(res ?? new Dictionary<string, double>());
        });

        return Ok(result);
    }
}