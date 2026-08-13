namespace WorldEngine.Domain.Simulation;

public static class NeedRates
{
    public const double HungerPerTick = 0.0025;

    public const double EnergyPerTick = 0.001;

    public const double SocialNeedPerTick = 0.0008;

    public const double HappinessUpdateRate = 0.05;

    public const double HealthRegenPerTick = 0.001;

    public const double HealthDamageFromStarvationPerTick = 0.003;

    public const double HealthDamageFromExhaustionPerTick = 0.002;

    public const double HealthDamageFromInjuryPerTick = 0.001;

    public const double HungerStarvationThreshold = 0.85;

    public const double HungerPanicThreshold = 0.65;

    public const double HungerUrgentThreshold = 0.45;

    public const double EnergyExhaustionThreshold = 0.05;

    public const double EnergyRestThreshold = 0.1;

    public const double HealthDeathThreshold = 0.0;

    public const int MaxAgeYears = 110;

    public const double OldAgeDeathBaseProbability = 0.0;

    public const double OldAgeDeathProbabilityPerYearOver40 = 0.00002;
}