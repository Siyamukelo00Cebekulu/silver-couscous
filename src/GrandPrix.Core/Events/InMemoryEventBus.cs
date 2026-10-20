using System;
using System.Collections.Generic;
using System.Linq;
using GrandPrix.Core.Abstractions;

namespace GrandPrix.Core.Events;

public sealed class InMemoryEventBus : IEventBus
{
    private readonly Dictionary<Type, List<Action<IDomainEvent>>> _subscriptions = new();

    public void Publish(IDomainEvent evt)
    {
        if (evt is null)
            throw new ArgumentNullException(nameof(evt));

        if (!_subscriptions.TryGetValue(evt.GetType(), out var handlers))
            return;

        foreach (var handler in handlers.ToArray())
            handler(evt);
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : IDomainEvent
    {
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);
        if (!_subscriptions.TryGetValue(eventType, out var handlers))
        {
            handlers = new List<Action<IDomainEvent>>();
            _subscriptions.Add(eventType, handlers);
        }

        Action<IDomainEvent> wrapped = evt => handler((TEvent)evt);
        handlers.Add(wrapped);
        return new Subscription(handlers, wrapped);
    }

    private sealed class Subscription : IDisposable
    {
        private List<Action<IDomainEvent>>? _handlers;
        private readonly Action<IDomainEvent> _handler;

        public Subscription(List<Action<IDomainEvent>> handlers, Action<IDomainEvent> handler)
        {
            _handlers = handlers;
            _handler = handler;
        }

        public void Dispose()
        {
            _handlers?.Remove(_handler);
            _handlers = null;
        }
    }
}
