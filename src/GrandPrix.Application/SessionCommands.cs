using System;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Common;

namespace GrandPrix.Application;

internal abstract class SessionCommand : ICommand
{
    protected SessionCommand(EntityId issuer) => Issuer = issuer;
    public EntityId Issuer { get; }
}

internal sealed class UpgradeFacilityAction : SessionCommand
{
    public UpgradeFacilityAction(EntityId issuer, string facility) : base(issuer) => Facility = facility;
    public string Facility { get; }
}

internal sealed class StartDevelopmentAction : SessionCommand
{
    public StartDevelopmentAction(EntityId issuer, string focus, long budgetCents) : base(issuer)
    {
        Focus = focus;
        BudgetCents = budgetCents;
    }

    public string Focus { get; }
    public long BudgetCents { get; }
}

internal sealed class AdvanceDevelopmentAction : SessionCommand
{
    public AdvanceDevelopmentAction(EntityId issuer) : base(issuer)
    {
    }
}

internal sealed class AdvanceCalendarAction : SessionCommand
{
    public AdvanceCalendarAction(EntityId issuer, int? days, bool toNextEvent) : base(issuer)
    {
        Days = days;
        ToNextEvent = toNextEvent;
    }

    public int? Days { get; }
    public bool ToNextEvent { get; }
}

internal sealed class RunPracticeAction : SessionCommand
{
    public RunPracticeAction(EntityId issuer, int seed, string focus) : base(issuer)
    {
        Seed = seed;
        Focus = focus;
    }

    public int Seed { get; }
    public string Focus { get; }
}

internal sealed class RunQualifyingAction : SessionCommand
{
    public RunQualifyingAction(EntityId issuer, int seed, string tyre, string risk, int runs) : base(issuer)
    {
        Seed = seed;
        Tyre = tyre;
        Risk = risk;
        Runs = runs;
    }

    public int Seed { get; }
    public string Tyre { get; }
    public string Risk { get; }
    public int Runs { get; }
}

internal sealed class StartRaceAction : SessionCommand
{
    public StartRaceAction(EntityId issuer, int laps, int seed) : base(issuer)
    {
        Laps = laps;
        Seed = seed;
    }

    public int Laps { get; }
    public int Seed { get; }
}

internal sealed class QueuePitStopAction : SessionCommand
{
    public QueuePitStopAction(EntityId issuer, int carNumber, string tyre, double extraDelaySeconds) : base(issuer)
    {
        CarNumber = carNumber;
        Tyre = tyre;
        ExtraDelaySeconds = extraDelaySeconds;
    }

    public int CarNumber { get; }
    public string Tyre { get; }
    public double ExtraDelaySeconds { get; }
}

internal sealed class SetRaceModeAction : SessionCommand
{
    public SetRaceModeAction(EntityId issuer, int carNumber, string mode) : base(issuer)
    {
        CarNumber = carNumber;
        Mode = mode;
    }

    public int CarNumber { get; }
    public string Mode { get; }
}

internal sealed class SettleLiveRaceAction : SessionCommand
{
    public SettleLiveRaceAction(EntityId issuer) : base(issuer)
    {
    }
}

internal sealed class SessionActionEvent : IDomainEvent
{
    public SessionActionEvent(DateTime occurredOn, string action, string message)
    {
        OccurredOn = occurredOn;
        Action = action;
        Message = message;
    }

    public DateTime OccurredOn { get; }
    public string Action { get; }
    public string Message { get; }
}

internal sealed class DelegateCommandHandler<TCommand> : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    private readonly Func<TCommand, CommandResult> _handle;

    public DelegateCommandHandler(Func<TCommand, CommandResult> handle)
    {
        _handle = handle ?? throw new ArgumentNullException(nameof(handle));
    }

    public CommandResult Handle(TCommand command) => _handle(command);
}
