using WorldEngine.Domain.Enums;

namespace WorldEngine.Api.Contracts;

public record WorldResponse(
    Guid Id,
    string Name,
    int RandomSeed,
    DateTime CurrentSimulationTime,
    double SimulationSpeed,
    SimulationStatus Status,
    long TickNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateWorldRequest(string Name, int? RandomSeed, int? InitialPopulation);

public record SetSimulationSpeedRequest(double Speed);

public record HealthResponse(string Status, string Version, DateTime Timestamp);