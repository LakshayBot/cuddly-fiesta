using Microsoft.EntityFrameworkCore;
using WorldEngine.Domain.Entities;

namespace WorldEngine.Infrastructure.Persistence;

public class WorldEngineDbContext : DbContext
{
    public WorldEngineDbContext(DbContextOptions<WorldEngineDbContext> options) : base(options)
    {
    }

    public DbSet<World> Worlds => Set<World>();

    public DbSet<Agent> Agents => Set<Agent>();

    public DbSet<SimulationEvent> SimulationEvents => Set<SimulationEvent>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<LocationResource> LocationResources => Set<LocationResource>();

    public DbSet<AgentInventory> AgentInventories => Set<AgentInventory>();

    public DbSet<AgentRelationship> AgentRelationships => Set<AgentRelationship>();

    public DbSet<AgentMemory> AgentMemories => Set<AgentMemory>();

    public DbSet<AgentDecisionRecord> AgentDecisionRecords => Set<AgentDecisionRecord>();

    public DbSet<Settlement> Settlements => Set<Settlement>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();

    public DbSet<EventCause> EventCauses => Set<EventCause>();

    public DbSet<EventConsequence> EventConsequences => Set<EventConsequence>();

    public DbSet<WorldHistoryEntry> WorldHistoryEntries => Set<WorldHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<World>(entity =>
        {
            entity.ToTable("worlds");
            entity.HasKey(w => w.Id);

            entity.Property(w => w.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(w => w.CurrentSimulationTime)
                .HasColumnType("timestamp with time zone");

            entity.Property(w => w.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(w => w.UpdatedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(w => w.Status)
                .HasConversion<int>();

            entity.HasIndex(w => w.Name);
        });

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.ToTable("agents");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(a => a.Location)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(a => a.Occupation)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(a => a.Money)
                .HasColumnType("numeric(18,2)");

            entity.Property(a => a.BirthSimulationTime)
                .HasColumnType("timestamp with time zone");

            entity.Property(a => a.DeathSimulationTime)
                .HasColumnType("timestamp with time zone");

            entity.Property(a => a.DeathCause)
                .HasMaxLength(200);

            entity.Property(a => a.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(a => a.UpdatedAt)
                .HasColumnType("timestamp with time zone");

            entity.HasIndex(a => a.WorldId);
            entity.HasIndex(a => new { a.WorldId, a.Alive });
        });

        modelBuilder.Entity<SimulationEvent>(entity =>
        {
            entity.ToTable("simulation_events");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Data)
                .HasColumnType("jsonb");

            entity.Property(e => e.SimulationTime)
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.HasIndex(e => new { e.WorldId, e.SimulationTime });
            entity.HasIndex(e => new { e.WorldId, e.EventType });
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("locations");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(l => l.Type)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(l => l.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(l => l.UpdatedAt)
                .HasColumnType("timestamp with time zone");

            entity.HasIndex(l => l.WorldId);
            entity.HasIndex(l => new { l.WorldId, l.Name }).IsUnique();
        });

        modelBuilder.Entity<LocationResource>(entity =>
        {
            entity.ToTable("location_resources");
            entity.HasKey(lr => new { lr.LocationId, lr.ResourceType });

            entity.Property(lr => lr.ResourceType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(lr => lr.UpdatedAt)
                .HasColumnType("timestamp with time zone");

            entity.HasOne<Location>()
                .WithMany()
                .HasForeignKey(lr => lr.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentInventory>(entity =>
        {
            entity.ToTable("agent_inventories");
            entity.HasKey(ai => new { ai.AgentId, ai.ResourceType });

            entity.Property(ai => ai.ResourceType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(ai => ai.UpdatedAt)
                .HasColumnType("timestamp with time zone");

            entity.HasOne<Agent>()
                .WithMany()
                .HasForeignKey(ai => ai.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentRelationship>(entity =>
        {
            entity.ToTable("agent_relationships");
            entity.HasKey(r => new { r.SourceAgentId, r.TargetAgentId });

            entity.Property(r => r.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(r => r.UpdatedAt)
                .HasColumnType("timestamp with time zone");

            entity.HasOne<Agent>()
                .WithMany()
                .HasForeignKey(r => r.SourceAgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentMemory>(entity =>
        {
            entity.ToTable("agent_memories");
            entity.HasKey(m => m.Id);

            entity.Property(m => m.Type)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(m => m.CreatedSimulationTime)
                .HasColumnType("timestamp with time zone");

            entity.Property(m => m.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(m => m.Summary)
                .IsRequired()
                .HasMaxLength(500);

            entity.HasIndex(m => new { m.AgentId, m.CreatedSimulationTime });
            entity.HasIndex(m => new { m.SimulationEventId, m.AgentId }).IsUnique();
        });

        modelBuilder.Entity<AgentDecisionRecord>(entity =>
        {
            entity.ToTable("agent_decisions");
            entity.HasKey(d => d.Id);

            entity.Property(d => d.DecisionSource)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(d => d.SelectedActionId)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(d => d.SelectedActionType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(d => d.AvailableActionsJson)
                .HasColumnType("jsonb");

            entity.Property(d => d.SimulationTime)
                .HasColumnType("timestamp with time zone");

            entity.Property(d => d.DecidedAt)
                .HasColumnType("timestamp with time zone");

            entity.Property(d => d.ModelName).HasMaxLength(100);
            entity.Property(d => d.PromptVersion).HasMaxLength(50);

            entity.HasIndex(d => new { d.AgentId, d.DecidedAt });
            entity.HasIndex(d => d.WorldId);
        });

        modelBuilder.Entity<Settlement>(entity =>
        {
            entity.ToTable("settlements");
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(s => s.CenterLocationName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(s => s.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(s => s.FormationReason)
                .HasMaxLength(2000);

            entity.Property(s => s.FirstPopulationAtTick)
                .HasColumnType("timestamp with time zone");

            entity.Property(s => s.CreationSimulationTime)
                .HasColumnType("timestamp with time zone");

            entity.Property(s => s.UpdatedAt)
                .HasColumnType("timestamp with time zone");

            entity.HasIndex(s => s.WorldId);
            entity.HasIndex(s => new { s.WorldId, s.CenterLocationName }).IsUnique();
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.ToTable("groups");
            entity.HasKey(g => g.Id);

            entity.Property(g => g.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(g => g.Type)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(g => g.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(g => g.FormationReason)
                .HasMaxLength(2000);

            entity.Property(g => g.FormationSimulationTime)
                .HasColumnType("timestamp with time zone");

            entity.Property(g => g.UpdatedAt)
                .HasColumnType("timestamp with time zone");

            entity.HasIndex(g => g.WorldId);
        });

        modelBuilder.Entity<GroupMembership>(entity =>
        {
            entity.ToTable("group_memberships");
            entity.HasKey(gm => new { gm.GroupId, gm.AgentId });

            entity.Property(gm => gm.Role)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(gm => gm.JoinedAt)
                .HasColumnType("timestamp with time zone");

            entity.HasOne<Group>()
                .WithMany()
                .HasForeignKey(gm => gm.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SimulationEvent>(entity =>
        {
            entity.Property(e => e.Importance)
                .HasConversion<int>();

            entity.HasIndex(e => new { e.WorldId, e.SimulationTime });
            entity.HasIndex(e => new { e.WorldId, e.EventType });
            entity.HasIndex(e => new { e.WorldId, e.Importance });
        });

        modelBuilder.Entity<EventCause>(entity =>
        {
            entity.ToTable("event_causes");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.CauseType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(c => c.Description)
                .HasMaxLength(1000);

            entity.HasIndex(c => c.EventId);
            entity.HasIndex(c => c.CauseEventId);
            entity.HasIndex(c => c.DecisionRecordId);
        });

        modelBuilder.Entity<EventConsequence>(entity =>
        {
            entity.ToTable("event_consequences");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Kind)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(c => c.ConsequenceType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(c => c.Description)
                .HasMaxLength(1000);

            entity.HasIndex(c => c.EventId);
            entity.HasIndex(c => c.ConsequenceEventId);
        });

        modelBuilder.Entity<WorldHistoryEntry>(entity =>
        {
            entity.ToTable("world_history_entries");
            entity.HasKey(h => h.Id);

            entity.Property(h => h.EntryType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(h => h.FactsJson)
                .HasColumnType("jsonb");

            entity.Property(h => h.Summary)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(h => h.SimulationTime)
                .HasColumnType("timestamp with time zone");

            entity.Property(h => h.CreatedAt)
                .HasColumnType("timestamp with time zone");

            entity.HasIndex(h => new { h.WorldId, h.SimulationTime });
            entity.HasIndex(h => new { h.WorldId, h.Importance });
        });
    }
}