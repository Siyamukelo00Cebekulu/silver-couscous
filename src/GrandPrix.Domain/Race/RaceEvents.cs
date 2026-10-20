using System;
using GrandPrix.Core.Common;

namespace GrandPrix.Domain.Race;

public abstract class RaceEvent
{
    public TimeSpan RaceTime { get; set; }
    public int Lap { get; set; }
    public abstract string Description { get; }
}

public sealed class OvertakeEvent : RaceEvent
{
    public EntityId Attacker { get; set; }
    public EntityId Defender { get; set; }
    public int NewPosition { get; set; }
    public string AttackerName { get; set; } = string.Empty;
    public string DefenderName { get; set; } = string.Empty;
    public override string Description => $"{AttackerName} passes {DefenderName}; now P{NewPosition}.";
}

public sealed class PitStopEvent : RaceEvent
{
    public EntityId CarId { get; set; }
    public double StationaryTimeSec { get; set; }
    public RaceTyreCompound Fitted { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public double PitLaneDelaySec { get; set; }
    public override string Description => $"{DriverName} pits for {Fitted}; {StationaryTimeSec:F1}s stop + {PitLaneDelaySec:F1}s delay.";
}

public sealed class IncidentEvent : RaceEvent
{
    public EntityId[] Involved { get; set; } = Array.Empty<EntityId>();
    public string Kind { get; set; } = string.Empty;
    public override string Description => $"Incident: {Kind}.";
}

public sealed class SafetyCarEvent : RaceEvent
{
    public bool Deployed { get; set; }
    public override string Description => Deployed ? "Safety car deployed." : "Safety car withdrawn.";
}

public sealed class FastestLapEvent : RaceEvent
{
    public EntityId CarId { get; set; }
    public double LapTimeSec { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public override string Description => $"{DriverName} sets fastest lap: {LapTimeSec:F3}s.";
}

public sealed class LapCompletedEvent : RaceEvent
{
    public EntityId CarId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public double LapTimeSec { get; set; }
    public override string Description => $"{DriverName} completes lap {Lap} in {LapTimeSec:F3}s.";
}

public sealed class RaceCompletedEvent : RaceEvent
{
    public override string Description => "Race finished.";
}

public sealed class CarFinishedEvent : RaceEvent
{
    public EntityId CarId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public int Position { get; set; }
    public override string Description => $"{DriverName} finishes in P{Position}.";
}
