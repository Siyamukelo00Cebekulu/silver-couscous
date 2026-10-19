using System;
using System.Collections.Generic;
using GrandPrix.Core.Abstractions;

namespace GrandPrix.Core.Common;

public sealed class CommandResult
{
    private CommandResult(
        bool succeeded,
        string? error,
        IReadOnlyList<IDomainEvent> events)
    {
        Succeeded = succeeded;
        Error = error;
        Events = events;
    }

    public bool Succeeded { get; }
    public string? Error { get; }
    public IReadOnlyList<IDomainEvent> Events { get; }

    public static CommandResult Ok(IReadOnlyList<IDomainEvent> events)
    {
        if (events is null)
            throw new ArgumentNullException(nameof(events));

        return new CommandResult(true, null, events);
    }

    public static CommandResult Fail(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("A failure reason is required.", nameof(error));

        return new CommandResult(false, error, Array.Empty<IDomainEvent>());
    }
}
