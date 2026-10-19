using System;
using System.Collections.Generic;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Common;

namespace GrandPrix.Domain.Race;

public interface IRaceEngine
{
    void Start(RaceSetup setup, IRandom random);
    void Submit(IRaceCommand command);
    IReadOnlyList<RaceEvent> Advance(TimeSpan elapsed);
    RaceSnapshot GetSnapshot();
    bool IsFinished { get; }
    RaceResult GetResult();
}

public sealed class RaceResult
{
    public IReadOnlyList<EntityId> Classification { get; set; } = Array.Empty<EntityId>();
    public IReadOnlyList<RaceEvent> FullEventLog { get; set; } = Array.Empty<RaceEvent>();
}
