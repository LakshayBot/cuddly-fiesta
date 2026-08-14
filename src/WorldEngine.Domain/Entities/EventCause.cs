namespace WorldEngine.Domain.Entities;

public static class EventCauseTypes
{
    public const string Decision = "Decision";
    public const string State = "State";
    public const string Event = "Event";
    public const string Resource = "Resource";
    public const string Relationship = "Relationship";
}

public static class EventConsequenceKinds
{
    public const string Direct = "Direct";
    public const string Indirect = "Indirect";
}

public static class EventConsequenceTypes
{
    public const string MemoryCreated = "MemoryCreated";
    public const string RelationshipChanged = "RelationshipChanged";
    public const string StateChanged = "StateChanged";
    public const string InventoryRedistributed = "InventoryRedistributed";
    public const string EventInfluenced = "EventInfluenced";
}

public class EventCause
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public string CauseType { get; set; } = string.Empty;

    public Guid? CauseEventId { get; set; }

    public Guid? DecisionRecordId { get; set; }

    public string Name { get; set; } = string.Empty;

    public double? Value { get; set; }

    public string Description { get; set; } = string.Empty;

    public long CreatedTick { get; set; }
}

public class EventConsequence
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string ConsequenceType { get; set; } = string.Empty;

    public Guid? ConsequenceEventId { get; set; }

    public Guid? ConsequenceMemoryId { get; set; }

    public string Description { get; set; } = string.Empty;

    public long CreatedTick { get; set; }
}