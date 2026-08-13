using WorldEngine.Domain.Simulation;

namespace WorldEngine.Domain.Emergence;

public static class EmergentNameGenerator
{
    private static readonly string[] Roots =
    {
        "Oak", "Elm", "Ash", "Birch", "Cedar", "Pine", "Willow", "Holly",
        "Rowan", "Maple", "Aspen", "Hazel", "Ivy", "Juniper", "Linden", "Myrtle",
    };

    private static readonly string[] SettlementSuffixes =
    {
        "stead", "haven", "wick", "ford", "field", "hollow", "grove", "dale",
        "shire", "cross", "bridge", "brook",
    };

    private static readonly string[] GroupSuffixes =
    {
        "kin", "band", "ring", "fold", "guild", "clan", "circle", "fellowship",
    };

    public static string Settlement(IRandomSource random)
    {
        var root = Roots[random.NextInt(0, Roots.Length)];
        var suffix = SettlementSuffixes[random.NextInt(0, SettlementSuffixes.Length)];
        return $"{root}{suffix}";
    }

    public static string Group(IRandomSource random, string type)
    {
        var root = Roots[random.NextInt(0, Roots.Length)];
        var suffix = GroupSuffixes[random.NextInt(0, GroupSuffixes.Length)];
        return $"{root}{suffix}";
    }
}