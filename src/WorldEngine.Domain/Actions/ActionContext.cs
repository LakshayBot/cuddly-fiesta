using WorldEngine.Domain.Entities;

namespace WorldEngine.Domain.Actions;

public sealed class ActionContext
{
    public ActionContext(
        Agent agent,
        World world,
        Simulation.SimulationContext simulation,
        Location currentLocation,
        IReadOnlyDictionary<Guid, Location> locationsById,
        IReadOnlyDictionary<string, Location> locationsByName,
        Dictionary<(Guid LocationId, string ResourceType), LocationResource> locationResources,
        Dictionary<(Guid AgentId, string ResourceType), AgentInventory> agentInventories,
        IReadOnlyList<Agent> nearbyAgents,
        List<SimulationEvent> newEvents,
        List<AgentInventory> pendingNewInventories,
        List<LocationResource> pendingNewLocationResources,
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
        NewEvents = newEvents;
        PendingNewInventories = pendingNewInventories;
        PendingNewLocationResources = pendingNewLocationResources;
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

    public List<SimulationEvent> NewEvents { get; }

    public List<AgentInventory> PendingNewInventories { get; }

    public List<LocationResource> PendingNewLocationResources { get; }

    public DateTime Now { get; }

    public double GetInventoryQuantity(string resourceType) =>
        AgentInventories.TryGetValue((Agent.Id, resourceType), out var inv) ? inv.Quantity : 0.0;

    public double GetLocationResourceQuantity(Guid locationId, string resourceType) =>
        LocationResources.TryGetValue((locationId, resourceType), out var lr) ? lr.Quantity : 0.0;

    public AgentInventory EnsureInventory(string resourceType)
    {
        var key = (Agent.Id, resourceType);
        if (AgentInventories.TryGetValue(key, out var existing))
        {
            return existing;
        }
        var newInv = new AgentInventory
        {
            AgentId = Agent.Id,
            ResourceType = resourceType,
            Quantity = 0,
            UpdatedAt = Now,
        };
        AgentInventories[key] = newInv;
        PendingNewInventories.Add(newInv);
        return newInv;
    }

    public LocationResource EnsureLocationResource(Guid locationId, string resourceType)
    {
        var key = (locationId, resourceType);
        if (LocationResources.TryGetValue(key, out var existing))
        {
            return existing;
        }
        var newLr = new LocationResource
        {
            LocationId = locationId,
            ResourceType = resourceType,
            Quantity = 0,
            UpdatedAt = Now,
        };
        LocationResources[key] = newLr;
        PendingNewLocationResources.Add(newLr);
        return newLr;
    }

    public AgentInventory EnsureInventoryFor(Guid agentId, string resourceType)
    {
        var key = (agentId, resourceType);
        if (AgentInventories.TryGetValue(key, out var existing))
        {
            return existing;
        }
        var newInv = new AgentInventory
        {
            AgentId = agentId,
            ResourceType = resourceType,
            Quantity = 0,
            UpdatedAt = Now,
        };
        AgentInventories[key] = newInv;
        PendingNewInventories.Add(newInv);
        return newInv;
    }

    public double GetOtherAgentInventoryQuantity(Guid agentId, string resourceType) =>
        AgentInventories.TryGetValue((agentId, resourceType), out var inv) ? inv.Quantity : 0.0;

    public SimulationEvent AppendEvent(string eventType, Guid? actorAgentId, Guid? targetAgentId, Guid? locationId, object data)
    {
        var evt = new SimulationEvent
        {
            Id = Guid.NewGuid(),
            WorldId = Agent.WorldId,
            Tick = Simulation.TickNumber,
            SimulationTime = Simulation.NewSimulationTime,
            EventType = eventType,
            ActorAgentId = actorAgentId,
            TargetAgentId = targetAgentId,
            LocationId = locationId,
            Data = System.Text.Json.JsonSerializer.Serialize(data),
            CreatedAt = Now,
        };
        NewEvents.Add(evt);
        return evt;
    }
}