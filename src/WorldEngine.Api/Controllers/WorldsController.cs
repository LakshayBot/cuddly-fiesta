using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldEngine.Api.Contracts;
using WorldEngine.Domain;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Enums;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;
using WorldEngine.Infrastructure.Population;
using WorldEngine.Infrastructure.Simulation;
using WorldEngine.Infrastructure.Worlds;

namespace WorldEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorldsController : ControllerBase
{
    private readonly WorldEngineDbContext _dbContext;
    private readonly RandomSourceRegistry _randomRegistry;
    private readonly PopulationGenerator _populationGenerator;
    private readonly SimulationOptions _simulationOptions;
    private readonly ILogger<WorldsController> _logger;

    public WorldsController(
        WorldEngineDbContext dbContext,
        RandomSourceRegistry randomRegistry,
        PopulationGenerator populationGenerator,
        SimulationOptions simulationOptions,
        ILogger<WorldsController> logger)
    {
        _dbContext = dbContext;
        _randomRegistry = randomRegistry;
        _populationGenerator = populationGenerator;
        _simulationOptions = simulationOptions;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorldResponse>>> List(CancellationToken cancellationToken)
    {
        var worlds = await _dbContext.Worlds
            .AsNoTracking()
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(worlds.Select(ToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorldResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var world = await _dbContext.Worlds
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (world is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(world));
    }

    [HttpPost]
    public async Task<ActionResult<WorldResponse>> Create([FromBody] CreateWorldRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Name is required." });
        }

        if (request.Name.Length > 200)
        {
            return BadRequest(new { error = "Name must be 200 characters or fewer." });
        }

        if (request.InitialPopulation is < 0 or > 1000)
        {
            return BadRequest(new { error = "InitialPopulation must be between 0 and 1000." });
        }

        var now = DateTime.UtcNow;
        var seed = request.RandomSeed ?? Random.Shared.Next();

        var world = new World
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            RandomSeed = seed,
            CurrentSimulationTime = now,
            SimulationSpeed = 1.0,
            Status = SimulationStatus.Paused,
            TickNumber = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _dbContext.Worlds.Add(world);

        var seeder = new WorldSeeder(_simulationOptions, now);
        var locations = seeder.SeedLocations(world);
        var locationResources = seeder.SeedLocationResources(locations);
        _dbContext.Locations.AddRange(locations);
        _dbContext.LocationResources.AddRange(locationResources);

        var population = request.InitialPopulation ?? 0;
        if (population > 0)
        {
            var random = _randomRegistry.GetOrCreate(world.Id, world.RandomSeed);
            var agents = _populationGenerator.Generate(world, population, random, now);
            _dbContext.Agents.AddRange(agents);

            foreach (var agent in agents)
            {
                _dbContext.SimulationEvents.Add(new SimulationEvent
                {
                    Id = Guid.NewGuid(),
                    WorldId = world.Id,
                    Tick = 0,
                    SimulationTime = world.CurrentSimulationTime,
                    EventType = SimulationEventTypes.AgentBorn,
                    ActorAgentId = null,
                    TargetAgentId = agent.Id,
                    LocationId = null,
                    Data = System.Text.Json.JsonSerializer.Serialize(new { name = agent.Name }),
                    CreatedAt = now,
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created world {WorldId} ({WorldName}) with seed {Seed} and {Population} agents",
            world.Id, world.Name, seed, population);

        return CreatedAtAction(nameof(GetById), new { id = world.Id }, ToResponse(world));
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<WorldResponse>> Start(Guid id, CancellationToken cancellationToken)
    {
        var world = await _dbContext.Worlds.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (world is null)
        {
            return NotFound();
        }

        if (world.Status == SimulationStatus.Running)
        {
            return Ok(ToResponse(world));
        }

        world.Status = SimulationStatus.Running;
        world.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Started world {WorldId}", world.Id);

        return Ok(ToResponse(world));
    }

    [HttpPost("{id:guid}/pause")]
    public async Task<ActionResult<WorldResponse>> Pause(Guid id, CancellationToken cancellationToken)
    {
        var world = await _dbContext.Worlds.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (world is null)
        {
            return NotFound();
        }

        if (world.Status == SimulationStatus.Paused)
        {
            return Ok(ToResponse(world));
        }

        world.Status = SimulationStatus.Paused;
        world.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Paused world {WorldId} at tick {Tick}", world.Id, world.TickNumber);

        return Ok(ToResponse(world));
    }

    [HttpPost("{id:guid}/stop")]
    public async Task<ActionResult<WorldResponse>> Stop(Guid id, CancellationToken cancellationToken)
    {
        var world = await _dbContext.Worlds.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (world is null)
        {
            return NotFound();
        }

        world.Status = SimulationStatus.Stopped;
        world.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stopped world {WorldId}", world.Id);

        return Ok(ToResponse(world));
    }

    [HttpPost("{id:guid}/speed")]
    public async Task<ActionResult<WorldResponse>> SetSpeed(Guid id, [FromBody] SetSimulationSpeedRequest request, CancellationToken cancellationToken)
    {
        if (double.IsNaN(request.Speed) || double.IsInfinity(request.Speed))
        {
            return BadRequest(new { error = "Speed must be a finite number." });
        }

        if (request.Speed < _simulationOptions.MinSimulationSpeed || request.Speed > _simulationOptions.MaxSimulationSpeed)
        {
            return BadRequest(new
            {
                error = $"Speed must be between {_simulationOptions.MinSimulationSpeed} and {_simulationOptions.MaxSimulationSpeed}.",
            });
        }

        var world = await _dbContext.Worlds.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (world is null)
        {
            return NotFound();
        }

        world.SimulationSpeed = request.Speed;
        world.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Set world {WorldId} speed to {Speed}", world.Id, world.SimulationSpeed);

        return Ok(ToResponse(world));
    }

    private static WorldResponse ToResponse(World world) =>
        new(
            world.Id,
            world.Name,
            world.RandomSeed,
            world.CurrentSimulationTime,
            world.SimulationSpeed,
            world.Status,
            world.TickNumber,
            world.CreatedAt,
            world.UpdatedAt);
}