using System.Linq;
using System.IO;
using GrandPrix.Application;
using GrandPrix.Core.Common;
using GrandPrix.Domain.Staff;
using GrandPrix.Persistence;

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

    [Fact]
    public void SaveAndLoadReplaysSessionAndContinuesRaceDeterministically()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"grand-prix-session-{System.Guid.NewGuid():N}");
        try
        {
            var factory = new GameSessionFactory(new FileSaveStore(directory));
            var session = (GameSession)factory.CreateNew();
            var candidate = session.GetStaff().Candidates[0];
            Assert.True(session.HireEngineer(
                candidate.MarketNumber,
                candidate.AskingAnnualSalaryCents,
                3).Succeeded);
            session.AdvanceDays(12);
            Assert.True(session.UpgradeFacilityView("wind").Succeeded);
            session.AdvanceToNextEvent();
            session.RunPracticeView(2026, "setup");
            session.RunQualifyingView(2027, "soft", "normal", 2);
            session.StartRace(4, 2028);
            session.AdvanceRace(15);
            Assert.True(session.QueuePitStop(11, "hard", 0).Succeeded);
            session.MarkInboxRead();
            factory.Save("continuation", session);

            var loaded = (GameSession)factory.Load("continuation");

            Assert.Equal(session.Today, loaded.Today);
            Assert.Equal(session.PlayerTeam.Budget.Cents, loaded.PlayerTeam.Budget.Cents);
            Assert.Equal(session.PlayerTeam.Engineers.Count, loaded.PlayerTeam.Engineers.Count);
            Assert.Equal(session.Teams.Select(team => team.Budget.Cents),
                loaded.Teams.Select(team => team.Budget.Cents));
            Assert.Equal(session.Teams.Select(team => team.Engineers.Count),
                loaded.Teams.Select(team => team.Engineers.Count));
            Assert.Equal(session.GetCalendar().Select(item => item.Completed),
                loaded.GetCalendar().Select(item => item.Completed));
            Assert.Equal(0, loaded.GetDashboard().UnreadInboxCount);
            AssertRaceTimingEqual(session.GetRaceTiming(), loaded.GetRaceTiming());

            while (!session.GetRaceTiming().Finished)
            {
                var originalTiming = session.AdvanceRace(60);
                var loadedTiming = loaded.AdvanceRace(60);
                AssertRaceTimingEqual(originalTiming, loadedTiming);
            }

            Assert.Contains(session.GetRaceTiming().Cars,
                car => !car.IsPlayerCar && car.PitStops > 0);
            var originalSettlement = session.SettleLiveRace();
            var loadedSettlement = loaded.SettleLiveRace();
            Assert.Equal(originalSettlement.Classification, loadedSettlement.Classification);
            Assert.Equal(originalSettlement.Payouts.Select(item => item.AmountCents),
                loadedSettlement.Payouts.Select(item => item.AmountCents));
            Assert.Equal(session.PlayerTeam.Budget.Cents, loaded.PlayerTeam.Budget.Cents);
            Assert.Equal(session.GetStandings().Select(item => item.Points),
                loaded.GetStandings().Select(item => item.Points));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AiRaceStrategyIsIndependentOfApplicationAdvanceStepSize()
    {
        var fine = RunRaceWithStepSize(1);
        var coarse = RunRaceWithStepSize(60);

        Assert.Equal(fine.Cars.Select(car => (car.CarNumber, car.Position)),
            coarse.Cars.Select(car => (car.CarNumber, car.Position)));
        Assert.Equal(fine.Cars.Select(car => (car.CarNumber, car.PitStops, car.Tyre)),
            coarse.Cars.Select(car => (car.CarNumber, car.PitStops, car.Tyre)));
    }

    private static RaceTimingViewModel RunRaceWithStepSize(double stepSeconds)
    {
        var session = new GameSession();
        session.AdvanceToNextEvent();
        session.RunPracticeView(2026, "setup");
        session.RunQualifyingView(2027, "soft", "normal", 2);
        session.StartRace(20, 2028);

        while (!session.GetRaceTiming().Finished)
            session.AdvanceRace(stepSeconds);

        return session.GetRaceTiming();
    }

    private static void AssertRaceTimingEqual(
        RaceTimingViewModel expected,
        RaceTimingViewModel actual)
    {
        Assert.Equal(expected.CurrentLap, actual.CurrentLap);
        Assert.Equal(expected.RaceTime, actual.RaceTime);
        Assert.Equal(expected.Cars.Select(car =>
                (car.CarNumber, car.Position, car.LapsCompleted, car.GapToLeaderSeconds, car.Tyre, car.TyreWear)),
            actual.Cars.Select(car =>
                (car.CarNumber, car.Position, car.LapsCompleted, car.GapToLeaderSeconds, car.Tyre, car.TyreWear)));
    }
}
