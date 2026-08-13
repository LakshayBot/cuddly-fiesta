namespace WorldEngine.Domain.Entities;

public class Agent
{
    public Guid Id { get; set; }

    public Guid WorldId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime BirthSimulationTime { get; set; }

    public bool Alive { get; set; }

    public DateTime? DeathSimulationTime { get; set; }

    public string? DeathCause { get; set; }

    public string Location { get; set; } = "Village";

    public string Occupation { get; set; } = "Unassigned";

    public decimal Money { get; set; }

    public double Hunger { get; set; }

    public double Energy { get; set; }

    public double Health { get; set; }

    public double Happiness { get; set; }

    public double Safety { get; set; }

    public double SocialNeed { get; set; }

    public double Curiosity { get; set; }

    public double Aggression { get; set; }

    public double Empathy { get; set; }

    public double Sociability { get; set; }

    public double Ambition { get; set; }

    public double RiskTolerance { get; set; }

    public double Discipline { get; set; }

    public double Generosity { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public double GetAgeYears(DateTime currentSimulationTime)
    {
        var endTime = Alive ? currentSimulationTime : (DeathSimulationTime ?? currentSimulationTime);
        return Math.Max(0.0, (endTime - BirthSimulationTime).TotalDays / 365.25);
    }
}