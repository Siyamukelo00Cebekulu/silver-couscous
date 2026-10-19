using System;
using GrandPrix.Core.Common;

namespace GrandPrix.Domain.Race;

public abstract class RaceEvent
{
    public TimeSpan RaceTime { get; set; }
    public int Lap { get; set; }
}

public sealed class OvertakeEvent : RaceEvent
{
    public EntityId Attacker { get; set; }
    public EntityId Defender { get; set; }
    public int NewPosition { get; set; }
}

public sealed class PitStopEvent : RaceEvent
{
    public EntityId CarId { get; set; }
    public double StationaryTimeSec { get; set; }
    public RaceTyreCompound Fitted { get; set; }
}

public sealed class IncidentEvent : RaceEvent
{
    public EntityId[] Involved { get; set; } = Array.Empty<EntityId>();
    public string Kind { get; set; } = string.Empty;
}

public sealed class SafetyCarEvent : RaceEvent
{
    public bool Deployed { get; set; }
}

public sealed class FastestLapEvent : RaceEvent
{
    public EntityId CarId { get; set; }
    public double LapTimeSec { get; set; }
}
