using System;
using System.Collections.Generic;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Common;

namespace GrandPrix.Core.Commands;

public sealed class CommandBus : ICommandBus
{
    private readonly IEventBus _eventBus;
    private readonly Dictionary<Type, Func<ICommand, CommandResult>> _handlers = new();

    public CommandBus(IEventBus eventBus)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public void Register<TCommand>(ICommandHandler<TCommand> handler)
        where TCommand : ICommand
    {
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        var commandType = typeof(TCommand);
        if (!_handlers.TryAdd(commandType, command => handler.Handle((TCommand)command)))
            throw new InvalidOperationException($"A handler is already registered for {commandType.Name}.");
    }

    public CommandResult Dispatch(ICommand command)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));
        if (!_handlers.TryGetValue(command.GetType(), out var handler))
            throw new InvalidOperationException($"No handler is registered for {command.GetType().Name}.");

        var result = handler(command);
        if (result.Succeeded)
        {
            foreach (var domainEvent in result.Events)
                _eventBus.Publish(domainEvent);
        }
        return result;
    }
}
