using System.Linq;
using GrandPrix.Core.Common;
using GrandPrix.Domain.Staff;

namespace GrandPrix.Domain.Tests;

public class GameSessionTests
{
    [Fact]
    public void HireCommandChecksSalaryAndPublishesEngineerHiredEvent()
    {
        var session = new GrandPrix.Application.GameSession();
        var candidate = session.EngineerCandidates[0];
        var originalBudget = session.PlayerTeam.Budget.Cents;

        var result = session.Execute(new HireEngineerCommand(
            session.PlayerTeam.Id,
            candidate.Id,
            candidate.AskingAnnualSalary,
            3));

        Assert.True(result.Succeeded, result.Error);
        Assert.Single(session.PlayerTeam.Engineers);
        Assert.Contains(result.Events, evt => evt is EngineerHiredEvent);
        Assert.True(session.PlayerTeam.Budget.Cents < originalBudget);
        Assert.Contains(session.Inbox, message => message.Contains(candidate.Name));
    }

    [Fact]
    public void HireCommandRejectsInsufficientSalaryAndInvalidContract()
    {
        var session = new GrandPrix.Application.GameSession();
        var candidate = session.EngineerCandidates[0];

        var lowOffer = session.Execute(new HireEngineerCommand(
            session.PlayerTeam.Id,
            candidate.Id,
            new Money(candidate.AskingAnnualSalary.Cents - 1),
            3));
        var invalidTerm = session.Execute(new HireEngineerCommand(
            session.PlayerTeam.Id,
            candidate.Id,
            candidate.AskingAnnualSalary,
            0));

        Assert.False(lowOffer.Succeeded);
        Assert.False(invalidTerm.Succeeded);
        Assert.Empty(session.PlayerTeam.Engineers);
    }

    [Fact]
    public void MonthlyPayrollIsPaidWhenCalendarReachesFirstDay()
    {
        var session = new GrandPrix.Application.GameSession();
        var originalBudget = session.PlayerTeam.Budget.Cents;
        var monthlyPayroll = session.PlayerTeam.MonthlyPayroll.Cents;

        for (var day = 0; day < 31; day++)
            session.AdvanceDay();

        Assert.Equal(new System.DateTime(2026, 4, 1), session.Today);
        Assert.Equal(originalBudget - monthlyPayroll, session.PlayerTeam.Budget.Cents);
        Assert.Contains(session.Inbox, message => message.Contains("payroll processed"));
    }
}
