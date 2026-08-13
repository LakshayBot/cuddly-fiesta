namespace WorldEngine.Domain;

public static class ResourceTypes
{
    public const string Food = "Food";
    public const string Wood = "Wood";
    public const string Water = "Water";

    public static readonly string[] All = { Food, Wood, Water };
}

public static class LocationTypes
{
    public const string Village = "Village";
    public const string Farm = "Farm";
    public const string Forest = "Forest";
    public const string River = "River";

    public static readonly string[] All = { Village, Farm, Forest, River };
}

public static class Occupations
{
    public const string Farmer = "Farmer";
    public const string Woodcutter = "Woodcutter";
    public const string Worker = "Worker";
    public const string Unemployed = "Unemployed";

    public static readonly string[] All = { Farmer, Woodcutter, Worker, Unemployed };
}

public static class ActionTypes
{
    public const string Eat = "Eat";
    public const string Rest = "Rest";
    public const string Move = "Move";
    public const string HarvestFood = "HarvestFood";
    public const string GatherWood = "GatherWood";
    public const string Work = "Work";
    public const string Idle = "Idle";
    public const string Talk = "Talk";
    public const string Help = "Help";
    public const string ShareFood = "ShareFood";
    public const string Trade = "Trade";
    public const string Steal = "Steal";
}

public static class SimulationEventTypes
{
    public const string AgentBorn = "AgentBorn";
    public const string AgentDied = "AgentDied";
    public const string AgentAte = "AgentAte";
    public const string AgentRested = "AgentRested";
    public const string AgentMoved = "AgentMoved";
    public const string AgentHarvestedFood = "AgentHarvestedFood";
    public const string AgentGatheredWood = "AgentGatheredWood";
    public const string AgentWorked = "AgentWorked";
    public const string AgentTalked = "AgentTalked";
    public const string AgentHelped = "AgentHelped";
    public const string AgentSharedFood = "AgentSharedFood";
    public const string AgentStole = "AgentStole";
    public const string AgentInsulted = "AgentInsulted";
    public const string AgentTraded = "AgentTraded";

    public const string SettlementFormed = "SettlementFormed";
    public const string GroupFormed = "GroupFormed";
    public const string ConflictOccurred = "ConflictOccurred";
}

public static class MemoryTypes
{
    public const string Talked = "Talked";
    public const string ReceivedHelp = "ReceivedHelp";
    public const string HelpedSomeone = "HelpedSomeone";
    public const string ReceivedFood = "ReceivedFood";
    public const string GaveFood = "GaveFood";
    public const string WitnessedDeath = "WitnessedDeath";
}

public static class RelationshipDefaults
{
    public const double Trust = 0.5;
    public const double Affection = 0.5;
    public const double Respect = 0.5;
    public const double Fear = 0.0;
    public const double Anger = 0.0;
    public const double Familiarity = 0.0;
}

public static class RelationshipDeltas
{
    public const double HelpTrust = 0.10;
    public const double HelpAffection = 0.08;
    public const double HelpRespect = 0.03;
    public const double HelpFamiliarity = 0.05;

    public const double ShareFoodTrust = 0.12;
    public const double ShareFoodAffection = 0.10;
    public const double ShareFoodRespect = 0.04;
    public const double ShareFoodFamiliarity = 0.04;

    public const double TalkFamiliarity = 0.02;
    public const double TalkAffection = 0.01;

    public const double StealTrust = -0.30;
    public const double StealAnger = 0.25;
    public const double StealRespect = -0.05;
    public const double StealFamiliarity = 0.05;

    public const double InsultAnger = 0.15;
    public const double InsultRespect = -0.10;
}

public static class MemoryImportance
{
    public const double Trivial = 0.1;
    public const double Minor = 0.25;
    public const double Notable = 0.55;
    public const double Significant = 0.8;
}

public static class TradeDefaults
{
    public const decimal FoodPrice = 0.5m;
}