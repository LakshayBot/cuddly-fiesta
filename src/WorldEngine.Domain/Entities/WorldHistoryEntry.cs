using WorldEngine.Domain.Enums;

namespace WorldEngine.Domain.Entities;

public class WorldHistoryEntry
{
    public Guid Id { get; set; }

    public Guid WorldId { get; set; }

    public long Tick { get; set; }

    public DateTime SimulationTime { get; set; }

    public string EntryType { get; set; } = string.Empty;

    public EventImportance Importance { get; set; }

    public string FactsJson { get; set; } = "{}";

    public string Summary { get; set; } = string.Empty;

    public Guid? RelatedEventId { get; set; }

    public DateTime CreatedAt { get; set; }
}

public static class WorldHistoryEntryTypes
{
    public const string SettlementFormed = "SettlementFormed";
    public const string GroupFormed = "GroupFormed";
    public const string PopulationDecline = "PopulationDecline";
    public const string FoodCrisis = "FoodCrisis";
    public const string MajorConflict = "MajorConflict";
    public const string DeathWave = "DeathWave";
    public const string FirstDeath = "FirstDeath";
}