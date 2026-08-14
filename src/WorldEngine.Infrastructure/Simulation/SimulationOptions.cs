namespace WorldEngine.Infrastructure.Simulation;

public sealed class SimulationOptions
{
    public int TickIntervalMs { get; set; } = 100;

    public double BaseSimSecondsPerTick { get; set; } = 60.0;

    public double MinSimulationSpeed { get; set; } = 0.0;

    public double MaxSimulationSpeed { get; set; } = 1000.0;

    public double FarmFoodRegenPerTick { get; set; } = 8.0;

    public double FarmFoodCapacity { get; set; } = 50.0;

    public double ForestWoodRegenPerTick { get; set; } = 5.0;

    public double ForestWoodCapacity { get; set; } = 50.0;

    public double RiverWaterRegenPerTick { get; set; } = 20.0;

    public double RiverWaterCapacity { get; set; } = 200.0;

    public double VillageFoodRegenPerTick { get; set; } = 0.0;

    public double VillageWoodRegenPerTick { get; set; } = 0.0;

    public double VillageWaterRegenPerTick { get; set; } = 0.0;

    public double VillageFoodCapacity { get; set; } = 200.0;

    public double VillageWoodCapacity { get; set; } = 200.0;

    public double VillageWaterCapacity { get; set; } = 500.0;

    public double VillageFoodSeed { get; set; } = 20.0;

    public double VillageWoodSeed { get; set; } = 10.0;

    public double VillageWaterSeed { get; set; } = 50.0;

    public double FarmFoodSeed { get; set; } = 10.0;

    public double ForestWoodSeed { get; set; } = 10.0;

    public double RiverWaterSeed { get; set; } = 100.0;

    public int MinSettlementPopulation { get; set; } = 8;

    public double SettlementPersistenceDays { get; set; } = 30.0;

    public int EmergenceInteractionWindowTicks { get; set; } = 200;

    public int MinGroupSize { get; set; } = 3;

    public double FamilyAffectionThreshold { get; set; } = 0.6;

    public double AllianceTrustThreshold { get; set; } = 0.6;

    public double ConflictAngerThreshold { get; set; } = 0.6;

    public double ConflictHungerThreshold { get; set; } = 0.8;

    public double ScarcityThreshold { get; set; } = 0.2;

    public int ConflictCooldownTicks { get; set; } = 50;

    public decimal TradeFoodPrice { get; set; } = 0.5m;

    public int CauseLookbackTicks { get; set; } = 100;

    public int ImportanceEscalationIntervalTicks { get; set; } = 120;

    public int HistoryWindowTicks { get; set; } = 200;

    public int HistoryEntryCooldownTicks { get; set; } = 150;

    public int DeathWaveThreshold { get; set; } = 5;

    public double PopulationDeclineThreshold { get; set; } = 0.2;

    public int MajorConflictThreshold { get; set; } = 3;

    public double FoodCrisisThreshold { get; set; } = 5.0;

    public int FoodCrisisWindowTicks { get; set; } = 100;
}