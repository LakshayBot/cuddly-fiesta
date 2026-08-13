using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorldEngine.Domain;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Emergence;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;
using WorldEngine.Infrastructure.Simulation;

namespace WorldEngine.Infrastructure.Simulation.Systems;

public sealed class GroupEmergenceSystem : ISimulationSystem
{
    private readonly IDbContextFactory<WorldEngineDbContext> _dbContextFactory;
    private readonly RandomSourceRegistry _randomRegistry;
    private readonly SimulationOptions _options;
    private readonly ILogger<GroupEmergenceSystem> _logger;

    public GroupEmergenceSystem(
        IDbContextFactory<WorldEngineDbContext> dbContextFactory,
        RandomSourceRegistry randomRegistry,
        SimulationOptions options,
        ILogger<GroupEmergenceSystem> logger)
    {
        _dbContextFactory = dbContextFactory;
        _randomRegistry = randomRegistry;
        _options = options;
        _logger = logger;
    }

    public async Task ProcessAsync(SimulationContext context, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var worldId = context.World.Id;

        var agents = await db.Agents
            .AsNoTracking()
            .Where(a => a.WorldId == worldId && a.Alive)
            .ToListAsync(cancellationToken);

        var relationships = await db.AgentRelationships
            .AsNoTracking()
            .Where(r => agents.Select(a => a.Id).Contains(r.SourceAgentId))
            .ToListAsync(cancellationToken);

        if (agents.Count == 0)
        {
            return;
        }

        var existingGroups = await db.Groups
            .AsNoTracking()
            .Where(g => g.WorldId == worldId)
            .ToListAsync(cancellationToken);

        var existingGroupIds = existingGroups.Select(g => g.Id).ToList();
        var existingMemberships = await db.GroupMemberships
            .AsNoTracking()
            .Where(gm => existingGroupIds.Contains(gm.GroupId))
            .ToListAsync(cancellationToken);

        var existingMemberSets = existingGroups.ToDictionary(
            g => g.Id,
            g => existingMemberships.Where(m => m.GroupId == g.Id).Select(m => m.AgentId).ToHashSet());

        var byType = new Dictionary<string, List<HashSet<Guid>>>();

        byType["Family"] = DetectMutualClusters(agents, relationships, r => r.Affection, _options.FamilyAffectionThreshold);
        byType["Alliance"] = DetectMutualClusters(agents, relationships, r => r.Trust, _options.AllianceTrustThreshold);
        byType["WorkGroup"] = DetectOccupationClusters(agents);

        var newEvents = new List<SimulationEvent>();
        var newGroups = new List<Group>();
        var newMemberships = new List<GroupMembership>();
        var random = _randomRegistry.GetOrCreate(worldId, context.World.RandomSeed);

        foreach (var (type, clusters) in byType)
        {
            foreach (var cluster in clusters)
            {
                if (cluster.Count < _options.MinGroupSize) continue;
                if (existingMemberSets.Values.Any(set => cluster.IsSubsetOf(set))) continue;

                var name = EmergentNameGenerator.Group(random, type);
                var explanation = BuildExplanation(type, cluster.Count, agents);

                var group = new Group
                {
                    Id = Guid.NewGuid(),
                    WorldId = worldId,
                    Name = name,
                    Type = type,
                    Status = "Forming",
                    FormationReason = explanation,
                    FormationSimulationTime = context.NewSimulationTime,
                    UpdatedAt = DateTime.UtcNow,
                };
                newGroups.Add(group);

                foreach (var agentId in cluster)
                {
                    newMemberships.Add(new GroupMembership
                    {
                        GroupId = group.Id,
                        AgentId = agentId,
                        Role = "Member",
                        JoinedAt = context.NewSimulationTime,
                    });
                }

                newEvents.Add(new SimulationEvent
                {
                    Id = Guid.NewGuid(),
                    WorldId = worldId,
                    Tick = context.TickNumber,
                    SimulationTime = context.NewSimulationTime,
                    EventType = SimulationEventTypes.GroupFormed,
                    ActorAgentId = null,
                    TargetAgentId = null,
                    LocationId = null,
                    Data = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        name,
                        type,
                        memberCount = cluster.Count,
                        members = cluster.Select(id => agents.FirstOrDefault(a => a.Id == id)?.Name ?? id.ToString()).ToArray(),
                        explanation,
                    }),
                    CreatedAt = DateTime.UtcNow,
                });

                _logger.LogInformation("Group {Name} ({Type}) formed in world {WorldId} with {Count} members",
                    name, type, worldId, cluster.Count);
            }
        }

        if (newGroups.Count > 0)
        {
            db.Groups.AddRange(newGroups);
        }
        if (newMemberships.Count > 0)
        {
            db.GroupMemberships.AddRange(newMemberships);
        }
        if (newEvents.Count > 0)
        {
            db.SimulationEvents.AddRange(newEvents);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static List<HashSet<Guid>> DetectMutualClusters(
        IReadOnlyList<Agent> agents,
        IReadOnlyList<AgentRelationship> relationships,
        Func<AgentRelationship, double> metric,
        double threshold)
    {
        var ids = agents.Select(a => a.Id).ToList();
        var idSet = ids.ToHashSet();

        var mutualPairs = relationships
            .Where(r => idSet.Contains(r.TargetAgentId) && metric(r) >= threshold)
            .Select(r => (From: r.SourceAgentId, To: r.TargetAgentId))
            .ToList();

        var reverse = mutualPairs
            .Select(p => (From: p.To, To: p.From))
            .ToHashSet();

        var mutual = mutualPairs.Where(p => reverse.Contains(p)).ToList();

        var dsu = new Dictionary<Guid, Guid>();
        Guid Find(Guid x)
        {
            if (!dsu.TryGetValue(x, out var root) || root == x) return x;
            dsu[x] = Find(root);
            return dsu[x];
        }

        void Union(Guid a, Guid b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb) dsu[ra] = rb;
        }

        foreach (var pair in mutual)
        {
            Union(pair.From, pair.To);
        }

        var clusters = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var id in ids)
        {
            var root = Find(id);
            if (!clusters.TryGetValue(root, out var set))
            {
                set = new HashSet<Guid>();
                clusters[root] = set;
            }
            set.Add(id);
        }

        return clusters.Values.ToList();
    }

    private static List<HashSet<Guid>> DetectOccupationClusters(IReadOnlyList<Agent> agents)
    {
        return agents
            .GroupBy(a => (a.Occupation, a.Location))
            .Where(g => g.Count() >= 3)
            .Select(g => g.Select(a => a.Id).ToHashSet())
            .ToList();
    }

    private static string BuildExplanation(string type, int memberCount, IReadOnlyList<Agent> agents)
    {
        var names = agents
            .OrderByDescending(a => a.Happiness)
            .Take(memberCount)
            .Select(a => a.Name)
            .ToArray();
        var who = string.Join(", ", names.Take(3));
        return $"{type} emerged from {memberCount} agents with mutual bonds. " +
               $"Members include {who}{(names.Length > 3 ? " and others" : "")}.";
    }
}