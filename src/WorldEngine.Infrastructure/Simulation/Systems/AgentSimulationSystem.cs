using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorldEngine.Domain;
using WorldEngine.Domain.Actions;
using WorldEngine.Domain.AI;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Simulation;
using WorldEngine.Infrastructure.Persistence;

namespace WorldEngine.Infrastructure.Simulation.Systems;

public sealed class AgentSimulationSystem : ISimulationSystem
{
    private static readonly IdleAction FallbackAction = new();

    private readonly IDbContextFactory<WorldEngineDbContext> _dbContextFactory;
    private readonly IAgentDecisionEngine _decisionEngine;
    private readonly ILogger<AgentSimulationSystem> _logger;

    public AgentSimulationSystem(
        IDbContextFactory<WorldEngineDbContext> dbContextFactory,
        IAgentDecisionEngine decisionEngine,
        ILogger<AgentSimulationSystem> logger)
    {
        _dbContextFactory = dbContextFactory;
        _decisionEngine = decisionEngine;
        _logger = logger;
    }

    public async Task ProcessAsync(SimulationContext context, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var agents = await db.Agents
            .Where(a => a.WorldId == context.World.Id && a.Alive)
            .ToListAsync(cancellationToken);

        if (agents.Count == 0)
        {
            return;
        }

        var locations = await db.Locations
            .Where(l => l.WorldId == context.World.Id)
            .ToListAsync(cancellationToken);

        if (locations.Count == 0)
        {
            return;
        }

        var locationsById = locations.ToDictionary(l => l.Id);
        var locationsByName = locations.ToDictionary(l => l.Name);

        var agentIds = agents.Select(a => a.Id).ToList();
        var inventories = await db.AgentInventories
            .Where(ai => agentIds.Contains(ai.AgentId))
            .ToListAsync(cancellationToken);
        var inventoryDict = inventories.ToDictionary(ai => (ai.AgentId, ai.ResourceType));

        var locationIds = locations.Select(l => l.Id).ToList();
        var resources = await db.LocationResources
            .Where(lr => locationIds.Contains(lr.LocationId))
            .ToListAsync(cancellationToken);
        var resourceDict = resources.ToDictionary(lr => (lr.LocationId, lr.ResourceType));

        var outgoingRelationships = await db.AgentRelationships
            .Where(r => agentIds.Contains(r.SourceAgentId))
            .ToListAsync(cancellationToken);
        var relationshipsByAgent = outgoingRelationships
            .GroupBy(r => r.SourceAgentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AgentRelationship>)g.ToList());

        var recentMemories = await db.AgentMemories
            .Where(m => agentIds.Contains(m.AgentId))
            .OrderByDescending(m => m.CreatedSimulationTime)
            .Take(200)
            .ToListAsync(cancellationToken);
        var memoriesByAgent = recentMemories
            .GroupBy(m => m.AgentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AgentMemory>)g.ToList());

        var newEvents = new List<SimulationEvent>();
        var newInventories = new List<AgentInventory>();
        var newLocationResources = new List<LocationResource>();
        var newDecisions = new List<AgentDecisionRecord>();
        var now = DateTime.UtcNow;

        var agentsByLocation = agents
            .GroupBy(a => a.Location)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Agent>)g.ToList());

        foreach (var agent in agents)
        {
            UpdateNeeds(agent);
            UpdateHappiness(agent);
            UpdateHealth(agent);

            if (!locationsByName.TryGetValue(agent.Location, out var currentLocation))
            {
                currentLocation = locationsByName.GetValueOrDefault(LocationTypes.Village)
                    ?? locations[0];
                agent.Location = currentLocation.Name;
            }

            var nearby = agentsByLocation.TryGetValue(agent.Location, out var peers)
                ? peers.Where(a => a.Id != agent.Id).ToList()
                : new List<Agent>();

            var actionContext = new ActionContext(
                agent: agent,
                world: context.World,
                simulation: context,
                currentLocation: currentLocation,
                locationsById: locationsById,
                locationsByName: locationsByName,
                locationResources: resourceDict,
                agentInventories: inventoryDict,
                nearbyAgents: nearby,
                newEvents: newEvents,
                pendingNewInventories: newInventories,
                pendingNewLocationResources: newLocationResources,
                now: now);

            relationshipsByAgent.TryGetValue(agent.Id, out var rels);
            rels ??= Array.Empty<AgentRelationship>();
            memoriesByAgent.TryGetValue(agent.Id, out var mems);
            mems ??= Array.Empty<AgentMemory>();

            var decisionContext = new AgentDecisionContext(
                agent: agent,
                world: context.World,
                simulation: context,
                currentLocation: currentLocation,
                locationsById: locationsById,
                locationsByName: locationsByName,
                locationResources: resourceDict,
                agentInventories: inventoryDict,
                nearbyAgents: nearby,
                outgoingRelationships: rels,
                recentMemories: mems,
                actionContext: actionContext,
                now: now);

            AgentDecision decision;
            try
            {
                decision = await _decisionEngine.DecideAsync(decisionContext, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decision engine failed for agent {AgentId}; falling back to Idle.", agent.Id);
                decision = new AgentDecision(
                    agentId: agent.Id,
                    decisionSource: AgentDecision.Sources.RuleBasedFallback,
                    availableActions: Array.Empty<ScoredAction>(),
                    selectedAction: new ScoredAction(ActionTypes.Idle, ActionTypes.Idle, new IdleAction(), 0, "Engine error fallback"),
                    reasoning: "Engine threw; fallback to Idle.",
                    decidedAt: now,
                    fallbackUsed: true);
            }

            var selectedCandidate = decision.SelectedAction;
            var actionToExecute = selectedCandidate is not null && selectedCandidate.Action.IsAvailable(actionContext)
                ? selectedCandidate.Action
                : FallbackAction;

            try
            {
                actionToExecute.Execute(actionContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Action {Action} failed for agent {AgentId}", actionToExecute.ActionType, agent.Id);
            }

            newDecisions.Add(BuildDecisionRecord(decision, worldId: context.World.Id, tick: context.TickNumber, now));

            if (ShouldDie(agent, context, context.Random))
            {
                Kill(agent, context, newEvents);
                ApplyDeathConsequences(agent, rels, agents, context);
            }

            agent.UpdatedAt = now;
        }

        if (newEvents.Count > 0)
        {
            db.SimulationEvents.AddRange(newEvents);
        }
        if (newInventories.Count > 0)
        {
            db.AgentInventories.AddRange(newInventories);
        }
        if (newLocationResources.Count > 0)
        {
            db.LocationResources.AddRange(newLocationResources);
        }
        if (newDecisions.Count > 0)
        {
            db.AgentDecisionRecords.AddRange(newDecisions);
        }

        foreach (var inv in inventoryDict.Values)
        {
            inv.UpdatedAt = now;
        }
        foreach (var res in resourceDict.Values)
        {
            res.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (newDecisions.Count > 0)
        {
            _logger.LogDebug(
                "Recorded {DecisionCount} decisions for world {WorldId} tick {Tick}",
                newDecisions.Count, context.World.Id, context.TickNumber);
        }
    }

    private static AgentDecisionRecord BuildDecisionRecord(AgentDecision decision, Guid worldId, long tick, DateTime now)
    {
        var actions = decision.AvailableActions
            .Select(a => new { id = a.ActionId, type = a.ActionType, score = Math.Round(a.Score, 3), reasoning = a.Reasoning })
            .ToArray();

        var selectedFactors = decision.SelectedAction?.Factors;
        var factorsJson = selectedFactors is { Count: > 0 }
            ? JsonSerializer.Serialize(selectedFactors.Select(f => new
            {
                type = f.Type.ToString(),
                name = f.Name,
                target = f.TargetName,
                value = Math.Round(f.Value, 3),
                contribution = Math.Round(f.Contribution, 3),
                description = f.Description,
            }))
            : null;

        return new AgentDecisionRecord
        {
            Id = Guid.NewGuid(),
            WorldId = worldId,
            AgentId = decision.AgentId,
            Tick = tick,
            SimulationTime = now,
            DecisionSource = decision.DecisionSource,
            SelectedActionId = decision.SelectedAction?.ActionId ?? ActionTypes.Idle,
            SelectedActionType = decision.SelectedAction?.ActionType ?? ActionTypes.Idle,
            SelectedScore = decision.SelectedAction?.Score ?? 0,
            AvailableActionsJson = JsonSerializer.Serialize(actions),
            SelectedFactorsJson = factorsJson,
            Reasoning = decision.Reasoning,
            DecidedAt = now,
            ModelName = decision.ModelName,
            PromptVersion = decision.PromptVersion,
            LatencyMs = decision.LatencyMs,
            FallbackUsed = decision.FallbackUsed,
        };
    }

    private static void UpdateNeeds(Agent agent)
    {
        agent.Hunger = Clamp01(agent.Hunger + NeedRates.HungerPerTick);
        agent.Energy = Clamp01(agent.Energy - NeedRates.EnergyPerTick);
        agent.SocialNeed = Clamp01(agent.SocialNeed + NeedRates.SocialNeedPerTick);
    }

    private static void UpdateHappiness(Agent agent)
    {
        var target = (
            (1.0 - agent.Hunger) +
            agent.Energy +
            agent.Health +
            agent.Safety +
            (1.0 - agent.SocialNeed)
        ) / 5.0;

        var delta = (target - agent.Happiness) * NeedRates.HappinessUpdateRate;
        agent.Happiness = Clamp01(agent.Happiness + delta);
    }

    private static void UpdateHealth(Agent agent)
    {
        var starving = agent.Hunger >= NeedRates.HungerStarvationThreshold;
        var exhausted = agent.Energy <= NeedRates.EnergyExhaustionThreshold;

        var damage = 0.0;
        if (starving) damage += NeedRates.HealthDamageFromStarvationPerTick;
        if (exhausted) damage += NeedRates.HealthDamageFromExhaustionPerTick;

        if (damage > 0)
        {
            agent.Health = Clamp01(agent.Health - damage);
        }
        else
        {
            agent.Health = Clamp01(agent.Health + NeedRates.HealthRegenPerTick);
        }
    }

    private static bool ShouldDie(Agent agent, SimulationContext context, IRandomSource random)
    {
        if (agent.Health <= NeedRates.HealthDeathThreshold)
        {
            return true;
        }

        var ageYears = agent.GetAgeYears(context.NewSimulationTime);
        if (ageYears >= NeedRates.MaxAgeYears)
        {
            return true;
        }

        if (ageYears > 40.0)
        {
            var yearsOver = ageYears - 40.0;
            var probability = yearsOver * NeedRates.OldAgeDeathProbabilityPerYearOver40;
            if (random.NextDouble() < probability)
            {
                return true;
            }
        }

        return false;
    }

    private static void Kill(Agent agent, SimulationContext context, List<SimulationEvent> events)
    {
        var ageYears = agent.GetAgeYears(context.NewSimulationTime);
        string cause;
        if (agent.Health <= NeedRates.HealthDeathThreshold)
        {
            cause = agent.Hunger >= NeedRates.HungerStarvationThreshold
                ? "Starvation"
                : "Exhaustion";
        }
        else if (ageYears >= NeedRates.MaxAgeYears)
        {
            cause = "Old age";
        }
        else
        {
            cause = "Natural causes";
        }

        agent.Alive = false;
        agent.DeathSimulationTime = context.NewSimulationTime;
        agent.DeathCause = cause;

        events.Add(new SimulationEvent
        {
            Id = Guid.NewGuid(),
            WorldId = agent.WorldId,
            Tick = context.TickNumber,
            SimulationTime = context.NewSimulationTime,
            EventType = SimulationEventTypes.AgentDied,
            ActorAgentId = null,
            TargetAgentId = agent.Id,
            LocationId = null,
            Data = JsonSerializer.Serialize(new
            {
                name = agent.Name,
                age = Math.Round(ageYears, 2),
                cause,
            }),
            CreatedAt = DateTime.UtcNow,
        });
    }

    private static void ApplyDeathConsequences(
        Agent agent,
        IReadOnlyList<AgentRelationship>? relationships,
        IReadOnlyList<Agent> allAgents,
        SimulationContext context)
    {
        var strongestPartner = relationships?
            .Where(r => r.Affection >= 0.7)
            .OrderByDescending(r => r.Affection)
            .FirstOrDefault();

        if (strongestPartner is null)
        {
            return;
        }

        var partner = allAgents.FirstOrDefault(a => a.Id == strongestPartner.TargetAgentId && a.Alive);
        if (partner is null)
        {
            return;
        }

        partner.Happiness = Clamp01(partner.Happiness - 0.12);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}