using System;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Common;

namespace GrandPrix.Domain.Staff;

public sealed class EngineerHiredEvent : IDomainEvent
{
    public EngineerHiredEvent(DateTime occurredOn, EntityId teamId, EntityId engineerId)
    {
        OccurredOn = occurredOn;
        TeamId = teamId;
        EngineerId = engineerId;
    }

    public DateTime OccurredOn { get; }
    public EntityId TeamId { get; }
    public EntityId EngineerId { get; }
}
