using GrandPrix.Core.Common;
using GrandPrix.Domain.Staff;
using GrandPrix.Domain.Team;

namespace GrandPrix.AI;

public sealed class ConservativeTeamPrincipalPolicy : ITeamPrincipalPolicy
{
    public HireEngineerCommand? ChooseAction(
        Team team,
        IReadOnlyList<EngineerCandidate> market)
    {
        if (team is null)
            throw new ArgumentNullException(nameof(team));
        if (market is null)
            throw new ArgumentNullException(nameof(market));

        foreach (var candidate in market
                     .OrderByDescending(item => item.Skill)
                     .ThenBy(item => item.AskingAnnualSalary.Cents)
                     .ThenBy(item => item.Id.ToString(), StringComparer.Ordinal))
        {
            if (team.Engineers.Any(engineer => engineer.Id == candidate.Id))
                continue;

            var monthlySalaryCents = candidate.AskingAnnualSalary.Cents / 12;
            var signingPayment = new Money(monthlySalaryCents);
            var payrollRunwayCents = checked(
                (team.MonthlyPayroll.Cents + monthlySalaryCents) * 6);
            if (team.Budget.Cents - signingPayment.Cents < payrollRunwayCents)
                continue;

            return new HireEngineerCommand(
                team.Id,
                candidate.Id,
                candidate.AskingAnnualSalary,
                contractYears: 3);
        }

        return null;
    }
}
