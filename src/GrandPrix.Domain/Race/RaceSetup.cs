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
    public IReadOnlyList<CarSetup> Grid { get; set; } = Array.Empty<CarSetup>();
    public IReadOnlyList<WeatherPoint> Forecast { get; set; } = Array.Empty<WeatherPoint>();
}

public sealed class CarSetup
{
    public EntityId CarId { get; set; }
    public EntityId TeamId { get; set; }
    public EntityId DriverId { get; set; }
    public RaceTyreCompound StartingTyre { get; set; }
    public double FuelLoadKg { get; set; }
}

public sealed class WeatherPoint
{
    public TimeSpan At { get; set; }
    public double RainIntensity { get; set; }
    public double TrackTempC { get; set; }
}
