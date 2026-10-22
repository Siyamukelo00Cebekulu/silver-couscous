using System.Text.Json.Serialization;

namespace GrandPrix.Data;

public sealed class GameContentDefinition
{
    [JsonRequired]
    public int SchemaVersion { get; set; }
    [JsonRequired]
    public DateTime StartDate { get; set; }
    [JsonRequired]
    public string PlayerTeamId { get; set; } = string.Empty;
    [JsonRequired]
    public List<TeamDefinition> Teams { get; set; } = new();
    [JsonRequired]
    public List<DriverDefinition> Drivers { get; set; } = new();
    [JsonRequired]
    public List<CircuitDefinition> Circuits { get; set; } = new();
    [JsonRequired]
    public List<EngineerCandidateDefinition> Engineers { get; set; } = new();
}

public sealed class TeamDefinition
{
    [JsonRequired]
    public string Id { get; set; } = string.Empty;
    [JsonRequired]
    public string Name { get; set; } = string.Empty;
    [JsonRequired]
    public long BudgetCents { get; set; }
    [JsonRequired]
    public long MonthlyBasePayrollCents { get; set; }
    [JsonRequired]
    public double CarPaceFactor { get; set; }
}

public sealed class DriverDefinition
{
    [JsonRequired]
    public string Id { get; set; } = string.Empty;
    [JsonRequired]
    public string TeamId { get; set; } = string.Empty;
    [JsonRequired]
    public string Name { get; set; } = string.Empty;
    [JsonRequired]
    public int Pace { get; set; }
    [JsonRequired]
    public int Consistency { get; set; }
    [JsonRequired]
    public int TyreManagement { get; set; }
    [JsonRequired]
    public int WetSkill { get; set; }
    [JsonRequired]
    public int Racecraft { get; set; }
    [JsonRequired]
    public int Focus { get; set; }
    [JsonRequired]
    public int Experience { get; set; }
    [JsonRequired]
    public int Feedback { get; set; }
    [JsonRequired]
    public int Aggression { get; set; }
    [JsonRequired]
    public int Defending { get; set; }
}

public sealed class CircuitDefinition
{
    [JsonRequired]
    public string Id { get; set; } = string.Empty;
    [JsonRequired]
    public string Name { get; set; } = string.Empty;
    [JsonRequired]
    public string Country { get; set; } = string.Empty;
    [JsonRequired]
    public int RaceLaps { get; set; }
    [JsonRequired]
    public double BaseLapTimeSeconds { get; set; }
    [JsonRequired]
    public double TyreWearFactor { get; set; }
    [JsonRequired]
    public double PitLossSeconds { get; set; }
    [JsonRequired]
    public double OvertakingDifficulty { get; set; }
    [JsonRequired]
    public string Characteristic { get; set; } = string.Empty;
}

public sealed class EngineerCandidateDefinition
{
    [JsonRequired]
    public string Id { get; set; } = string.Empty;
    [JsonRequired]
    public string Name { get; set; } = string.Empty;
    [JsonRequired]
    public long AskingAnnualSalaryCents { get; set; }
    [JsonRequired]
    public int Skill { get; set; }
}
