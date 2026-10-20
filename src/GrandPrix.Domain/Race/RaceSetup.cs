using System;
using System.Collections.Generic;
using GrandPrix.Core.Common;

namespace GrandPrix.Domain.Race;

public enum RaceTyreCompound
{
    Soft,
    Medium,
    Hard,
    Intermediate,
    Wet
}

public enum DriverMode
{
    Push,
    Standard,
    SaveTyres,
    SaveFuel
}

public sealed class RaceSetup
{
    public string TrackId { get; set; } = string.Empty;
    public int TotalLaps { get; set; }
    public int Seed { get; set; }
    public double BaseLapTimeSeconds { get; set; } = 90;
    public double TyreWearFactor { get; set; } = 1;
    public double PitLossTimeSeconds { get; set; } = 22;
    public double OvertakingDifficulty { get; set; } = 1;
    public IReadOnlyList<CarSetup> Grid { get; set; } = Array.Empty<CarSetup>();
    public IReadOnlyList<WeatherPoint> Forecast { get; set; } = Array.Empty<WeatherPoint>();
}

public sealed class CarSetup
{
    public EntityId CarId { get; set; }
    public EntityId TeamId { get; set; }
    public EntityId DriverId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public int CarNumber { get; set; }
    public int DriverPace { get; set; } = 70;
    public int Consistency { get; set; } = 70;
    public int TyreManagement { get; set; } = 70;
    public int Racecraft { get; set; } = 70;
    public int Focus { get; set; } = 70;
    public int WetSkill { get; set; } = 70;
    public int Aggression { get; set; } = 60;
    public int Defending { get; set; } = 70;
    public double CarPaceFactor { get; set; } = 1;
    public RaceTyreCompound StartingTyre { get; set; }
    public double FuelLoadKg { get; set; }
    public bool IsAi { get; set; }
    public int AiPitLap { get; set; }
}

public sealed class WeatherPoint
{
    public TimeSpan At { get; set; }
    public double RainIntensity { get; set; }
    public double TrackTempC { get; set; }
}
