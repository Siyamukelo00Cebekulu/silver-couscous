using System;
using System.Collections.Generic;
using GrandPrix.Core.Common;

namespace GrandPrix.Domain.Race;

public sealed class RaceSnapshot
{
    public TimeSpan RaceTime { get; set; }
    public int CurrentLap { get; set; }
    public bool SafetyCarActive { get; set; }
    public double RainIntensity { get; set; }
    public IReadOnlyList<CarState> Cars { get; set; } = Array.Empty<CarState>();
}

public sealed class CarState
{
    public EntityId CarId { get; set; }
    public int Position { get; set; }
    public double TrackProgress { get; set; }
    public double GapToLeaderSec { get; set; }
    public double GapToAheadSec { get; set; }
    public RaceTyreCompound Tyre { get; set; }
    public double TyreWear { get; set; }
    public double FuelKg { get; set; }
    public DriverMode Mode { get; set; }
    public bool InPit { get; set; }
    public bool Retired { get; set; }
}
