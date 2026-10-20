using System;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Common;

namespace GrandPrix.Application;

public sealed class PayrollPaidEvent : IDomainEvent
{
    public PayrollPaidEvent(DateTime occurredOn, EntityId teamId, Money amount)
    {
        OccurredOn = occurredOn;
        TeamId = teamId;
        Amount = amount;
    }

    public DateTime OccurredOn { get; }
    public EntityId TeamId { get; }
    public Money Amount { get; }
}
