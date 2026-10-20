using System;
using System.Collections.Generic;
using System.Linq;
using GrandPrix.Application;
using GrandPrix.Core.Common;
using GrandPrix.Core.Simulation;
using GrandPrix.Domain.Calendar;
using GrandPrix.Domain.Drivers;
using GrandPrix.Domain.Race;
using GrandPrix.Domain.Staff;
using TeamAggregate = GrandPrix.Domain.Team.Team;

var checks = new (string Name, Action Run)[]
{
    ("seeded random sequence repeatability", Checks.SeededRandomIsRepeatable),
    ("race repeatability by seed", Checks.RaceIsRepeatable),
    ("game session seed repeatability across launches", Checks.SessionRaceIsRepeatable),
    ("independent per-car random streams", Checks.CarStreamsAreIndependent),
    ("lap progress and timing gaps update during a tick", Checks.ProgressAndGapsUpdate),
    ("pit stop, pace instruction, and finish events", Checks.PitAndFinishEvents),
    ("overtake, classification, and progress", Checks.OvertakesUpdateStandings),
    ("salary validation and hiring event", Checks.HiringValidatesAndPublishes),
    ("six-month payroll runway validation", Checks.HiringProtectsPayrollRunway),
    ("monthly payroll timing", Checks.PayrollRunsAtMonthBoundary),
    ("race payout updates team cash and championship points", Checks.RacePrizeSettlement),
    ("legacy race simulator repeatability", Checks.LegacySimulatorIsRepeatable)
};

var failures = 0;
foreach (var check in checks)
{
    try
    {
        check.Run();
        Console.WriteLine($"PASS {check.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {check.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{checks.Length - failures}/{checks.Length} checks passed.");
return failures == 0 ? 0 : 1;

internal static class Checks
{
    public static void SeededRandomIsRepeatable()
    {
        var first = new SeededRandom(42);
        var second = new SeededRandom(42);
        Check(first.Next(0, 1000) == second.Next(0, 1000), "Integer draws differed.");
        Check(first.NextDouble() == second.NextDouble(), "Floating-point draws differed.");
        Check(first.NextGaussian(0, 1) == second.NextGaussian(0, 1), "Gaussian draws differed.");

        var firstChild = new SeededRandom(42).Fork("tyres");
        var secondParent = new SeededRandom(42);
        secondParent.NextDouble();
        var secondChild = secondParent.Fork("tyres");
        Check(firstChild.NextDouble() == secondChild.NextDouble(), "A parent draw changed a forked stream.");
    }

    public static void RaceIsRepeatable()
    {
        var firstSetup = CreateSetup();
        var secondSetup = CreateSetup(firstSetup);
        var first = RunToFinish(firstSetup, 83).GetResult();
        var second = RunToFinish(secondSetup, 83).GetResult();

        Check(first.Classification.SequenceEqual(second.Classification), "Classification changed for the same seed.");
        var firstLapTimes = first.FullEventLog.OfType<LapCompletedEvent>().Select(item => item.LapTimeSec);
        var secondLapTimes = second.FullEventLog.OfType<LapCompletedEvent>().Select(item => item.LapTimeSec);
        Check(firstLapTimes.SequenceEqual(secondLapTimes), "Lap times changed for the same seed.");
    }

    public static void CarStreamsAreIndependent()
    {
        var setupWithTwo = CreateSetup();
        var soloSetup = CreateSetup(setupWithTwo);
        soloSetup.Grid = new[] { soloSetup.Grid[0] };
        var duel = RunToFinish(setupWithTwo, 19).GetResult();
        var solo = RunToFinish(soloSetup, 19).GetResult();

        var duelLapTimes = duel.FullEventLog.OfType<LapCompletedEvent>()
            .Where(item => item.CarId == setupWithTwo.Grid[0].CarId)
            .Select(item => item.LapTimeSec);
        var soloLapTimes = solo.FullEventLog.OfType<LapCompletedEvent>().Select(item => item.LapTimeSec);
        Check(soloLapTimes.SequenceEqual(duelLapTimes), "Adding a car changed another car's random sequence.");
    }

    public static void ProgressAndGapsUpdate()
    {
        var setup = CreateSetup();
        var engine = new LapRaceEngine();
        engine.Start(setup, new SeededRandom(27));
        engine.Advance(TimeSpan.FromSeconds(25));
        var snapshot = engine.GetSnapshot();

        Check(snapshot.Cars.Any(car => car.TrackProgress > 0 && car.TrackProgress < 1),
            "Cars did not expose in-lap progress.");
        Check(snapshot.Cars.All(car => car.GapToLeaderSec >= 0 && car.GapToAheadSec >= 0),
            "A timing gap was negative.");
    }

    public static void SessionRaceIsRepeatable()
    {
        static RaceResult Play()
        {
            var setup = new GameSession().CreateRaceSetup(5, 314);
            var engine = new LapRaceEngine();
            engine.Start(setup, new SeededRandom(314));
            while (!engine.IsFinished)
                engine.AdvanceOneLap();
            return engine.GetResult();
        }

        var first = Play();
        var second = Play();
        Check(first.Classification.SequenceEqual(second.Classification),
            "A new game session changed the race classification for the same seed.");
        Check(first.FullEventLog.OfType<LapCompletedEvent>().Select(item => item.LapTimeSec)
                .SequenceEqual(second.FullEventLog.OfType<LapCompletedEvent>().Select(item => item.LapTimeSec)),
            "A new game session changed the race lap times for the same seed.");
    }

    public static void PitAndFinishEvents()
    {
        var setup = CreateSetup();
        setup.TotalLaps = 4;
        var engine = new LapRaceEngine();
        engine.Start(setup, new SeededRandom(8));
        var player = setup.Grid[0];
        engine.Submit(new BoxThisLapCommand(player.CarId, RaceTyreCompound.Hard, 3));
        engine.Submit(new SetDriverModeCommand(player.CarId, DriverMode.Push));

        while (engine.GetSnapshot().Cars.Single(car => car.CarId == player.CarId).LapsCompleted == 0)
            engine.AdvanceOneLap();
        Check(engine.GetSnapshot().Cars.Any(car => car.CarId == player.CarId &&
            car.Tyre == RaceTyreCompound.Hard && car.Mode == DriverMode.Push), "Queued commands did not apply.");
        while (!engine.IsFinished)
            engine.AdvanceOneLap();

        var events = engine.GetResult().FullEventLog;
        Check(events.OfType<PitStopEvent>().Any(item => item.CarId == player.CarId &&
            Math.Abs(item.PitLaneDelaySec - setup.PitLossTimeSeconds - 3) < 0.000001), "Pit loss/delay was not recorded.");
        Check(events.OfType<CarFinishedEvent>().Count() == setup.Grid.Count, "Not every car finished.");
        Check(events.OfType<RaceCompletedEvent>().Any(), "Race completion event is missing.");
    }

    public static void OvertakesUpdateStandings()
    {
        var setup = CreateSetup();
        setup.Grid[0].CarPaceFactor = 1.08;
        setup.Grid[1].CarPaceFactor = 0.9;
        setup.TotalLaps = 3;
        var engine = RunToFinish(setup, 12);
        var snapshot = engine.GetSnapshot();

        Check(engine.GetResult().FullEventLog.OfType<OvertakeEvent>().Any(), "No pass event was generated.");
        Check(snapshot.Cars[0].CarId == setup.Grid[1].CarId, "The faster car did not lead the classification.");
        Check(snapshot.Cars[0].Position == 1 && snapshot.Cars[0].TrackProgress == 1,
            "Finished standings did not report position and progress.");
        Check(snapshot.Cars[1].GapToAheadSec >= 0, "Gap to the car ahead must not be negative.");
    }

    public static void HiringValidatesAndPublishes()
    {
        var session = new GameSession();
        var candidate = session.EngineerCandidates[0];
        var lowOffer = session.Execute(new HireEngineerCommand(
            session.PlayerTeam.Id,
            candidate.Id,
            new Money(candidate.AskingAnnualSalary.Cents - 1),
            3));
        Check(!lowOffer.Succeeded, "Below-asking salary was accepted.");

        var hired = session.Execute(new HireEngineerCommand(
            session.PlayerTeam.Id,
            candidate.Id,
            candidate.AskingAnnualSalary,
            3));
        Check(hired.Succeeded, hired.Error ?? "Valid hire was rejected.");
        Check(hired.Events.OfType<EngineerHiredEvent>().Any(), "Hiring event was not returned.");
        Check(session.PlayerTeam.Engineers.Count == 1, "Engineer was not added to the team.");
    }

    public static void PayrollRunsAtMonthBoundary()
    {
        var session = new GameSession();
        var originalCash = session.PlayerTeam.Budget.Cents;
        var monthlyPayroll = session.PlayerTeam.MonthlyPayroll.Cents;

        for (var day = 0; day < 31; day++)
            session.AdvanceDay();

        Check(session.Today == new DateTime(2026, 4, 1), "Clock did not advance to the first of the next month.");
        Check(session.PlayerTeam.Budget.Cents == originalCash - monthlyPayroll,
            "Payroll was not deducted exactly once at the month boundary.");
    }

    public static void HiringProtectsPayrollRunway()
    {
        var team = new TeamAggregate(EntityId.New(), "Small Budget Racing", new Money(10_000), new Money(1_000));
        var candidate = new EngineerCandidate(EntityId.New(), "Budget Test Engineer", new Money(12_000), 70);
        var handler = new HireEngineerHandler(team, new[] { candidate }, new ManualGameClock(new DateTime(2026, 1, 1)));
        var result = handler.Handle(new HireEngineerCommand(
            team.Id,
            candidate.Id,
            candidate.AskingAnnualSalary,
            1));

        Check(!result.Succeeded, "Hire was accepted despite leaving less than six months of payroll.");
        Check(team.Engineers.Count == 0, "Rejected hire still changed team staff.");
        Check(team.Budget.Cents == 10_000, "Rejected hire still changed team cash.");
    }

    public static void RacePrizeSettlement()
    {
        var session = new GameSession();
        var setup = session.CreateRaceSetup(3, 42);
        var engine = new LapRaceEngine();
        engine.Start(setup, new SeededRandom(42));
        while (!engine.IsFinished)
            engine.AdvanceOneLap();

        var oldCash = session.PlayerTeam.Budget.Cents;
        var payouts = session.SettleRace(setup, engine.GetResult());
        var playerPayout = payouts.Single(item => item.IsPlayer);

        Check(playerPayout.Amount.Cents > 0, "Player team received no prize money.");
        Check(session.PlayerTeam.Budget.Cents == oldCash + playerPayout.Amount.Cents,
            "Prize payout did not increase team cash.");
        Check(session.PlayerTeam.ChampionshipPoints > 0, "Race points were not added to the team.");
    }

    public static void LegacySimulatorIsRepeatable()
    {
        var track = new Track { Name = "Legacy Test", BaseLapTime = 90, Laps = 5, TyreWearFactor = 1, PitLossTime = 20 };
        var driver = new Driver { Name = "Legacy Driver" };
        var plan = new StrategyPlan
        {
            StartingTyre = TyreCompoundType.Medium,
            PitLap = 3,
            PitTyre = TyreCompoundType.Hard
        };
        var first = new RaceSimulator(track, 7).Simulate(driver, plan);
        var second = new RaceSimulator(track, 7).Simulate(driver, plan);
        Check(first.TotalTime == second.TotalTime && first.PitStops == second.PitStops,
            "Existing race simulator is no longer deterministic.");
    }

    private static LapRaceEngine RunToFinish(RaceSetup setup, int seed)
    {
        var engine = new LapRaceEngine();
        engine.Start(setup, new SeededRandom(seed));
        while (!engine.IsFinished)
            engine.AdvanceOneLap();
        return engine;
    }

    private static RaceSetup CreateSetup(RaceSetup? copy = null)
    {
        var first = copy?.Grid[0].CarId ?? EntityId.New();
        var second = copy?.Grid[1].CarId ?? EntityId.New();
        var firstTeam = copy?.Grid[0].TeamId ?? EntityId.New();
        var secondTeam = copy?.Grid[1].TeamId ?? EntityId.New();
        var firstDriver = copy?.Grid[0].DriverId ?? EntityId.New();
        var secondDriver = copy?.Grid[1].DriverId ?? EntityId.New();
        return new RaceSetup
        {
            TrackId = "test-circuit",
            TotalLaps = 5,
            BaseLapTimeSeconds = 90,
            TyreWearFactor = 1,
            PitLossTimeSeconds = 18,
            Grid = new[]
            {
                new CarSetup
                {
                    CarId = first, TeamId = firstTeam, DriverId = firstDriver, CarNumber = 1,
                    DriverName = "Driver One", TeamName = "Team One", DriverPace = 77,
                    Consistency = 80, TyreManagement = 75, StartingTyre = RaceTyreCompound.Medium
                },
                new CarSetup
                {
                    CarId = second, TeamId = secondTeam, DriverId = secondDriver, CarNumber = 2,
                    DriverName = "Driver Two", TeamName = "Team Two", DriverPace = 84,
                    Consistency = 86, TyreManagement = 80, StartingTyre = RaceTyreCompound.Soft
                }
            }
        };
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
