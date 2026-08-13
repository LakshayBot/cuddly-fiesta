using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using WorldEngine.Api;
using WorldEngine.Api.Contracts;
using WorldEngine.Domain.AI;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.AI;
using WorldEngine.Infrastructure.Persistence;
using WorldEngine.Infrastructure.Population;
using WorldEngine.Infrastructure.Simulation;
using WorldEngine.Infrastructure.Simulation.Systems;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var corsOrigins = builder.Configuration["Cors:Origins"];
if (!string.IsNullOrWhiteSpace(corsOrigins))
{
    var origins = corsOrigins
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));
}

var connectionString = builder.Configuration.GetConnectionString("WorldEngine")
    ?? throw new InvalidOperationException("Connection string 'WorldEngine' is not configured.");

var contextOptions = new DbContextOptionsBuilder<WorldEngineDbContext>()
    .UseNpgsql(connectionString)
    .Options;
builder.Services.AddSingleton(contextOptions);

builder.Services.AddScoped<WorldEngineDbContext>(sp =>
    new WorldEngineDbContext(sp.GetRequiredService<DbContextOptions<WorldEngineDbContext>>()));

builder.Services.AddSingleton<IDbContextFactory<WorldEngineDbContext>>(sp =>
    new SimpleDbContextFactory(sp.GetRequiredService<DbContextOptions<WorldEngineDbContext>>()));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<WorldEngineDbContext>("database");

var simulationOptions = new SimulationOptions
{
    TickIntervalMs = builder.Configuration.GetValue("Simulation:TickIntervalMs", 100),
    BaseSimSecondsPerTick = builder.Configuration.GetValue("Simulation:BaseSimSecondsPerTick", 60.0),
    MinSimulationSpeed = builder.Configuration.GetValue("Simulation:MinSimulationSpeed", 0.0),
    MaxSimulationSpeed = builder.Configuration.GetValue("Simulation:MaxSimulationSpeed", 1000.0),
    FarmFoodRegenPerTick = builder.Configuration.GetValue("Simulation:FarmFoodRegenPerTick", 8.0),
    FarmFoodCapacity = builder.Configuration.GetValue("Simulation:FarmFoodCapacity", 50.0),
    ForestWoodRegenPerTick = builder.Configuration.GetValue("Simulation:ForestWoodRegenPerTick", 5.0),
    ForestWoodCapacity = builder.Configuration.GetValue("Simulation:ForestWoodCapacity", 50.0),
    RiverWaterRegenPerTick = builder.Configuration.GetValue("Simulation:RiverWaterRegenPerTick", 20.0),
    RiverWaterCapacity = builder.Configuration.GetValue("Simulation:RiverWaterCapacity", 200.0),
    VillageFoodSeed = builder.Configuration.GetValue("Simulation:VillageFoodSeed", 20.0),
    VillageWoodSeed = builder.Configuration.GetValue("Simulation:VillageWoodSeed", 10.0),
    VillageWaterSeed = builder.Configuration.GetValue("Simulation:VillageWaterSeed", 50.0),
    FarmFoodSeed = builder.Configuration.GetValue("Simulation:FarmFoodSeed", 10.0),
    ForestWoodSeed = builder.Configuration.GetValue("Simulation:ForestWoodSeed", 10.0),
    RiverWaterSeed = builder.Configuration.GetValue("Simulation:RiverWaterSeed", 100.0),
};
builder.Services.AddSingleton(simulationOptions);

builder.Services.AddSingleton<RandomSourceRegistry>();
builder.Services.AddSingleton<PopulationGenerator>();
builder.Services.AddSingleton<IActionGenerator, ActionGenerator>();
builder.Services.AddSingleton<RuleBasedDecisionEngine>();
builder.Services.AddSingleton<ILLMClient, NullLLMClient>();
builder.Services.AddSingleton<IAgentDecisionEngine>(sp =>
{
    var generator = sp.GetRequiredService<IActionGenerator>();
    var fallback = sp.GetRequiredService<RuleBasedDecisionEngine>();
    var client = sp.GetRequiredService<ILLMClient>();
    return new LLMDecisionEngine(generator, client, fallback);
});
builder.Services.AddSingleton<ISimulationSystem, LocationRegenSystem>();
builder.Services.AddSingleton<ISimulationSystem, AgentSimulationSystem>();
builder.Services.AddSingleton<ISimulationSystem, SocialConsequenceSystem>();
builder.Services.AddSingleton<ISimulationSystem, SettlementEmergenceSystem>();
builder.Services.AddSingleton<ISimulationSystem, GroupEmergenceSystem>();
builder.Services.AddSingleton<ISimulationSystem, ConflictDetectionSystem>();
builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddSingleton<ISimulationBroadcaster, SignalRSimulationBroadcaster>();

builder.Services.AddSignalR();

builder.Services.AddHostedService<SimulationLoopService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorldEngineDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!string.IsNullOrWhiteSpace(corsOrigins))
{
    app.UseCors();
}

app.MapGet("/", () => Results.Ok(new
{
    name = "WorldEngine.Api",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
}));

app.MapGet("/health", () => Results.Ok(new HealthResponse(
        Status: "ok",
        Version: Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
        Timestamp: DateTime.UtcNow)))
    .WithName("Health");

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.MapControllers();
app.MapHub<SimulationHub>("/hubs/simulation");

app.Run();

public partial class Program;