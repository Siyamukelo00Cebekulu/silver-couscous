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

/// <summary>Provides the normalized 0-1 tyre wear calculation shared by simulation and strategy advice.</summary>
public static class RaceTyreModel
{
    /// <summary>Returns normalized tyre wear added by one lap for a compound and driver.</summary>
    public static double WearPerLap(RaceTyreCompound tyre, double wearFactor, int tyreManagement)
    {
        if (!Enum.IsDefined(typeof(RaceTyreCompound), tyre))
            throw new ArgumentOutOfRangeException(nameof(tyre));
        if (double.IsNaN(wearFactor) || double.IsInfinity(wearFactor) || wearFactor < 0)
            throw new ArgumentOutOfRangeException(nameof(wearFactor));
        if (tyreManagement < 0 || tyreManagement > 100)
            throw new ArgumentOutOfRangeException(nameof(tyreManagement));

        var compoundWear = tyre switch
        {
            RaceTyreCompound.Soft => 3.2,
            RaceTyreCompound.Medium => 2.2,
            RaceTyreCompound.Hard => 1.5,
            RaceTyreCompound.Intermediate => 2.7,
            RaceTyreCompound.Wet => 2.4,
            _ => throw new ArgumentOutOfRangeException(nameof(tyre))
        };
        return compoundWear * wearFactor *
            (1 - tyreManagement / 100.0 * 0.3) / 100.0;
    }
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
