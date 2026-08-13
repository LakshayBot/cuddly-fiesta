using WorldEngine.Domain;
using WorldEngine.Domain.Entities;
using WorldEngine.Domain.Simulation;

namespace WorldEngine.Infrastructure.Population;

public sealed class PopulationGenerator
{
    private static readonly string[] FirstNames =
    {
        "Aria", "Bjorn", "Cedric", "Dahlia", "Elara", "Finn", "Greta", "Hugo",
        "Iris", "Joren", "Kira", "Lior", "Mira", "Nox", "Oren", "Petra",
        "Quinn", "Ronan", "Selene", "Tomas", "Ulrich", "Vera", "Wren", "Xanthe",
        "Yara", "Zane", "Anwen", "Bran", "Calla", "Doran", "Eira", "Faelan",
        "Gareth", "Hilde", "Idris", "Jora", "Kestrel", "Larkin", "Maeve", "Nyla",
        "Orin", "Pia", "Quill", "Rune", "Saoirse", "Theron", "Una", "Vesper",
        "Wynn", "Yusuf", "Zara", "Bren", "Cassia", "Daven", "Elin", "Florian",
        "Galen", "Halia", "Ivor", "Juno", "Kael", "Linnea", "Marek", "Nessa",
        "Osric", "Perrin", "Quint", "Rhys", "Sable", "Talin", "Ula", "Vance",
        "Wilder", "Xerxes", "Yvette", "Zephyr", "Alaric", "Briar", "Corin", "Dalla",
        "Esme", "Fenris", "Glynda", "Hesper", "Ilona", "Jessamy", "Korin", "Llewelyn",
    };

    private static readonly string[] LastNames =
    {
        "Adams", "Brennan", "Calloway", "Darrow", "Emberlin", "Fairwind", "Greymane", "Halloran",
        "Ironwood", "Jardine", "Kettering", "Lockwood", "Marsh", "Northcott", "Oakhart", "Pellington",
        "Quince", "Ravensdale", "Stonemarch", "Thistlewood", "Underhill", "Vance", "Whitlow", "Yardley",
        "Ashford", "Blackwell", "Crowley", "Dunwood", "Ellsworth", "Fenwick", "Gladstone", "Hartley",
        "Ingleton", "Jephson", "Kenwick", "Larkfield", "Montgomery", "Norwich", "Osmond", "Pendragon",
        "Quartermaine", "Rookwood", "Sutcliffe", "Thornton", "Ulverston", "Vickers", "Wickham", "Yarborough",
        "Aldridge", "Brackenbury", "Coldwell", "Drumfield", "Edgecombe", "Foxglove", "Gildersleeve", "Hartshorn",
        "Inglewood", "Jardine", "Kingsford", "Larkrise", "Millhaven", "Norwood", "Oatfield", "Penrose",
        "Quickenden", "Redbourn", "Stonebridge", "Tresham", "Upwood", "Verdley", "Wickenden", "Yarmouth",
        "Ashcroft", "Brockwell", "Carrickfergus", "Donnington", "Eastleigh", "Farnham", "Gisborne", "Hawksmoor",
    };

    private static readonly (string Occupation, double Weight)[] OccupationDistribution =
    {
        (Occupations.Farmer, 0.55),
        (Occupations.Woodcutter, 0.25),
        (Occupations.Worker, 0.10),
        (Occupations.Unemployed, 0.10),
    };

    public IReadOnlyList<Agent> Generate(
        World world,
        int count,
        IRandomSource random,
        DateTime now)
    {
        if (count <= 0)
        {
            return Array.Empty<Agent>();
        }

        var agents = new List<Agent>(count);
        for (var i = 0; i < count; i++)
        {
            agents.Add(CreateAgent(world, random, now, i));
        }

        return agents;
    }

    private static Agent CreateAgent(World world, IRandomSource random, DateTime now, int index)
    {
        var firstName = FirstNames[random.NextInt(0, FirstNames.Length)];
        var lastName = LastNames[random.NextInt(0, LastNames.Length)];
        var name = $"{firstName} {lastName}";

        var ageYears = SampleAgeYears(random);
        var birth = world.CurrentSimulationTime.AddDays(-ageYears * 365.25);

        var occupation = SampleOccupation(random);

        var money = Math.Round((decimal)(5.0 + random.NextDouble() * 95.0), 2);

        return new Agent
        {
            Id = Guid.NewGuid(),
            WorldId = world.Id,
            Name = name,
            BirthSimulationTime = birth,
            Alive = true,
            Location = "Village",
            Occupation = occupation,
            Money = money,
            Hunger = 0.2 + random.NextDouble() * 0.2,
            Energy = 0.7 + random.NextDouble() * 0.3,
            Health = 0.9 + random.NextDouble() * 0.1,
            Happiness = 0.6 + random.NextDouble() * 0.3,
            Safety = 0.95,
            SocialNeed = 0.3 + random.NextDouble() * 0.3,
            Curiosity = SampleTrait(random),
            Aggression = SampleTrait(random),
            Empathy = SampleTrait(random),
            Sociability = SampleTrait(random),
            Ambition = SampleTrait(random),
            RiskTolerance = SampleTrait(random),
            Discipline = SampleTrait(random),
            Generosity = SampleTrait(random),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static double SampleAgeYears(IRandomSource random)
    {
        var bucket = random.NextDouble();
        return bucket switch
        {
            < 0.15 => 2.0 + random.NextDouble() * 12.0,
            < 0.35 => 14.0 + random.NextDouble() * 16.0,
            < 0.85 => 30.0 + random.NextDouble() * 30.0,
            < 0.97 => 60.0 + random.NextDouble() * 15.0,
            _ => 75.0 + random.NextDouble() * 15.0,
        };
    }

    private static string SampleOccupation(IRandomSource random)
    {
        var roll = random.NextDouble();
        var cumulative = 0.0;
        foreach (var (occupation, weight) in OccupationDistribution)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                return occupation;
            }
        }
        return OccupationDistribution[^1].Occupation;
    }

    private static double SampleTrait(IRandomSource random)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        var gaussian = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        var value = 0.5 + gaussian * 0.15;
        return Math.Clamp(value, 0.0, 1.0);
    }
}