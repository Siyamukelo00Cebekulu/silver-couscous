using System;
using System.Collections.Generic;
using System.Linq;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Common;
using TeamAggregate = GrandPrix.Domain.Team.Team;

namespace GrandPrix.Domain.Staff;

public sealed class HireEngineerHandler : ICommandHandler<HireEngineerCommand>
{
    private readonly TeamAggregate _team;
    private readonly IReadOnlyList<EngineerCandidate> _candidates;
    private readonly IClock _clock;

    public HireEngineerHandler(
        TeamAggregate team,
        IReadOnlyList<EngineerCandidate> candidates,
        IClock clock)
    {
        _team = team ?? throw new ArgumentNullException(nameof(team));
        _candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public CommandResult Handle(HireEngineerCommand command)
    {
        if (command.Issuer != _team.Id)
            return CommandResult.Fail("Only this team's principal can authorize the contract.");
        if (command.ContractYears < 1 || command.ContractYears > 5)
            return CommandResult.Fail("Contract length must be between 1 and 5 years.");
        if (command.OfferedSalary.Cents <= 0)
            return CommandResult.Fail("Annual salary must be greater than zero.");

        var candidate = _candidates.FirstOrDefault(item => item.Id == command.CandidateId);
        if (candidate is null)
            return CommandResult.Fail("That engineer is not currently on the market.");
        if (_team.Engineers.Any(engineer => engineer.Id == candidate.Id))
            return CommandResult.Fail("That engineer has already joined your team.");
        if (command.OfferedSalary.Cents < candidate.AskingAnnualSalary.Cents)
            return CommandResult.Fail("The offer is below the candidate's asking salary.");
        if (command.OfferedSalary.Cents > candidate.AskingAnnualSalary.Cents * 3 / 2)
            return CommandResult.Fail("The offer exceeds the 150% salary negotiation limit.");

        var monthlySalary = command.OfferedSalary.Cents / 12;
        var signingPayment = new Money(monthlySalary);
        var sixMonthRunway = checked((_team.MonthlyPayroll.Cents + monthlySalary) * 6);
        if (checked(_team.Budget.Cents - signingPayment.Cents) < sixMonthRunway)
            return CommandResult.Fail("Hiring would leave less than six months of payroll runway.");

        _team.Deduct(signingPayment);
        _team.Engineers.Add(new Engineer(
            candidate.Id,
            candidate.Name,
            command.OfferedSalary,
            command.ContractYears));
        return CommandResult.Ok(new IDomainEvent[]
        {
            new EngineerHiredEvent(_clock.Today, _team.Id, candidate.Id)
        });
    }
}
