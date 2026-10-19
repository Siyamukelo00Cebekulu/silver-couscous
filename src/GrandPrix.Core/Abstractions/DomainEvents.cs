using System;

namespace GrandPrix.Core.Abstractions;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

public interface IEventBus
{
    void Publish(IDomainEvent evt);
    IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : IDomainEvent;
}
