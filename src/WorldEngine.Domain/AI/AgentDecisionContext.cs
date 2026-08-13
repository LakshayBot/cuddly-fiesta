using WorldEngine.Domain.Actions;
using WorldEngine.Domain.Entities;

namespace WorldEngine.Domain.AI;

public sealed class AgentDecisionContext
{
    public AgentDecisionContext(
        Agent agent,
        World world,
        Simulation.SimulationContext simulation,
        Location currentLocation,
        IReadOnlyDictionary<Guid, Location> locationsById,
        IReadOnlyDictionary<string, Location> locationsByName,
        Dictionary<(Guid LocationId, string ResourceType), LocationResource> locationResources,
        Dictionary<(Guid AgentId, string ResourceType), AgentInventory> agentInventories,
        IReadOnlyList<Agent> nearbyAgents,
        IReadOnlyList<AgentRelationship> outgoingRelationships,
        IReadOnlyList<AgentMemory> recentMemories,
        ActionContext actionContext,
        DateTime now)
    {
        Agent = agent;
        World = world;
        Simulation = simulation;
        CurrentLocation = currentLocation;
        LocationsById = locationsById;
        LocationsByName = locationsByName;
        LocationResources = locationResources;
        AgentInventories = agentInventories;
        NearbyAgents = nearbyAgents;
        OutgoingRelationships = outgoingRelationships;
        RecentMemories = recentMemories;
        ActionContext = actionContext;
        Now = now;
    }

    public Agent Agent { get; }

    public World World { get; }

    public Simulation.SimulationContext Simulation { get; }

    public Location CurrentLocation { get; }

    public IReadOnlyDictionary<Guid, Location> LocationsById { get; }

    public IReadOnlyDictionary<string, Location> LocationsByName { get; }

    public Dictionary<(Guid LocationId, string ResourceType), LocationResource> LocationResources { get; }

    public Dictionary<(Guid AgentId, string ResourceType), AgentInventory> AgentInventories { get; }

    public IReadOnlyList<Agent> NearbyAgents { get; }

    public IReadOnlyList<AgentRelationship> OutgoingRelationships { get; }

    public IReadOnlyList<AgentMemory> RecentMemories { get; }

    public ActionContext ActionContext { get; }

    public DateTime Now { get; }

    public double GetInventoryQuantity(string resourceType) =>
        ActionContext.GetInventoryQuantity(resourceType);

    public double GetOtherAgentInventoryQuantity(Guid otherAgentId, string resourceType) =>
        AgentInventories.TryGetValue((otherAgentId, resourceType), out var inv) ? inv.Quantity : 0.0;

    public double GetLocationResourceQuantity(Guid locationId, string resourceType) =>
        ActionContext.GetLocationResourceQuantity(locationId, resourceType);

    public AgentRelationship? FindRelationshipWith(Guid otherAgentId) =>
        OutgoingRelationships.FirstOrDefault(r => r.TargetAgentId == otherAgentId);
}