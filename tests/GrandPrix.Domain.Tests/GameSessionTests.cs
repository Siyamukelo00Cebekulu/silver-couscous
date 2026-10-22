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

    [Fact]
    public void DashboardAndManagementScreensExposePresentationData()
    {
        var session = new GrandPrix.Application.GameSession();

        var dashboard = session.GetDashboard();
        var calendar = session.GetCalendar();
        var standings = session.GetStandings();
        var staff = session.GetStaff();
        var facilities = session.GetFacilities();
        var development = session.GetDevelopment();

        Assert.Equal("Apex GP", dashboard.TeamName);
        Assert.Equal("Caspian Park Grand Prix", dashboard.NextCircuit);
        Assert.Equal(5, dashboard.RoundsRemaining);
        Assert.Equal(1, dashboard.ChampionshipPosition);
        Assert.Equal(0, dashboard.ChampionshipPoints);
        Assert.Equal(0, dashboard.StaffCount);
        Assert.Equal(2, dashboard.UnreadInboxCount);
        Assert.Equal(5, calendar.Count);
        Assert.Contains(calendar, item => item.Next && item.Circuit == dashboard.NextCircuit);
        Assert.Equal(5, standings.Count);
        Assert.True(standings[0].IsPlayer);
        Assert.Empty(staff.Engineers);
        Assert.Equal(4, staff.Candidates.Count);
        Assert.Equal(1, facilities.WindTunnelLevel);
        Assert.True(facilities.WindTunnelUpgradeCostCents > 0);
        Assert.False(development.HasActiveProject);
        Assert.Equal(500_000_000, development.CostCapHeadroomCents);

        session.MarkInboxRead();
        Assert.Equal(0, session.GetDashboard().UnreadInboxCount);
        Assert.All(session.GetInbox(), item => Assert.False(item.Unread));
    }

    [Fact]
    public void HiringByMarketNumberUsesTheCommandBusAndRejectsInvalidSelection()
    {
        var session = new GrandPrix.Application.GameSession();

        var candidateSalary = session.GetStaff().Candidates[0].AskingAnnualSalaryCents;
        var rejected = session.HireEngineer(0, candidateSalary, 3);
        var hired = session.HireEngineer(1, candidateSalary, 3);

        Assert.False(rejected.Succeeded);
        Assert.Contains("market", rejected.Error, System.StringComparison.OrdinalIgnoreCase);
        Assert.True(hired.Succeeded, hired.Error);
        Assert.Single(session.GetStaff().Engineers);
        Assert.True(session.GetStaff().Candidates[0].Hired);
    }

    [Fact]
    public void CalendarAdvanceStopsAtTheNextEventAndReportsProgress()
    {
        var session = new GrandPrix.Application.GameSession();

        var week = session.AdvanceDays(7);
        var eventWeek = session.AdvanceToNextEvent();

        Assert.Equal(7, week.DaysAdvanced);
        Assert.Equal(new System.DateTime(2026, 3, 8), week.Today);
        Assert.True(eventWeek.ReachedNextEvent);
        Assert.Equal(session.GetCalendar()[0].Date, eventWeek.Today);
        Assert.Equal(28, eventWeek.DaysAdvanced);
    }
}
