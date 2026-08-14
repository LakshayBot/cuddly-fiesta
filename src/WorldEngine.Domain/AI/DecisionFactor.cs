namespace WorldEngine.Domain.AI;

public enum FactorType
{
    Baseline,
    Need,
    Personality,
    Relationship,
    State,
    Memory,
    Opportunity,
}

public sealed record DecisionFactor(
    FactorType Type,
    string Name,
    string? TargetName,
    double Value,
    double Contribution,
    string Description);

public static class DecisionFactorExtensions
{
    public static string Describe(this DecisionFactor f) =>
        $"{f.Name}{(f.TargetName is null ? "" : $":{f.TargetName}")} {f.Value:0.##} → {f.Contribution:+0.##;-0.##;0} ({f.Type})";
}