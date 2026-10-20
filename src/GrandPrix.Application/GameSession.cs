using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Commands;
using GrandPrix.Core.Common;
using GrandPrix.Core.Events;
using GrandPrix.Core.Simulation;
using GrandPrix.Domain.Calendar;
using GrandPrix.Domain.Car;
using GrandPrix.Domain.Drivers;
using GrandPrix.Domain.Facilities;
using GrandPrix.Domain.Race;
using GrandPrix.Domain.Staff;
using GrandPrix.Domain.Team;

namespace GrandPrix.Application;

public sealed class GameSession : IGameSession
{
    private static readonly int[] PointsByPosition = { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 };
    private static readonly long[] PrizeCentsByPosition =
    {
        200_000_000, 150_000_000, 120_000_000, 95_000_000, 75_000_000,
        60_000_000, 45_000_000, 30_000_000, 20_000_000, 10_000_000
    };
    private readonly InMemoryEventBus _eventBus = new();
    private readonly CommandBus _commandBus;
    private readonly ManualGameClock _clock;
    private readonly List<string> _inbox = new();
    private readonly Dictionary<EntityId, int> _driverPoints = new();
    private readonly List<Driver> _drivers = new();
    private readonly Dictionary<EntityId, Team> _teamsById = new();
    private readonly IReadOnlyList<EngineerCandidate> _engineerCandidates;
    private readonly List<CalendarRaceEvent> _calendar;
    private readonly Dictionary<Team, CarDesign> _carDesigns = new();
    private DevelopmentProject? _developmentProject;

    public GameSession()
    {
        _clock = new ManualGameClock(new DateTime(2026, 3, 1));
        PlayerTeam = new Team(
            StableId("team:apex-gp"),
            "Apex GP",
            new Money(4_200_000_000),
            new Money(180_000_000));
        Teams = new List<Team>
        {
            PlayerTeam,
            new(StableId("team:aurora-motorsport"), "Aurora Motorsport", new Money(5_000_000_000), new Money(200_000_000)),
            new(StableId("team:cinder-racing"), "Cinder Racing", new Money(3_800_000_000), new Money(165_000_000)),
            new(StableId("team:northstar-works"), "Northstar Works", new Money(4_600_000_000), new Money(190_000_000)),
            new(StableId("team:velox-engineering"), "Velox Engineering", new Money(3_500_000_000), new Money(155_000_000))
        };
        foreach (var team in Teams)
            _teamsById.Add(team.Id, team);
        foreach (var team in Teams)
            _carDesigns.Add(team, new CarDesign());
        Facilities = new TeamFacilities();
        Circuits = new[]
        {
            new RaceCircuit("caspian-park", "Caspian Park Grand Prix", "Azerbaijan", 16, 96.4, 1.05, 19.5, 0.8, "Fast street circuit"),
            new RaceCircuit("silverstone", "Silverstone Grand Prix", "United Kingdom", 18, 91.8, 1.12, 21, 0.7, "High-speed and aerodynamic"),
            new RaceCircuit("monza", "Monza Grand Prix", "Italy", 18, 84.2, 0.92, 18, 0.55, "Low-drag power circuit"),
            new RaceCircuit("suzuka", "Suzuka Grand Prix", "Japan", 17, 94.6, 1.18, 22, 0.9, "Technical, high commitment"),
            new RaceCircuit("marina-bay", "Marina Bay Grand Prix", "Singapore", 15, 103.2, 1.28, 24, 1.15, "Hot, demanding street circuit")
        };
        _calendar = Circuits.Select((circuit, index) =>
            new CalendarRaceEvent(_clock.Today.AddDays(35 + index * 35), circuit)).ToList();
        Weekend = new RaceWeekend(NextEvent.Circuit.Name);

        Drivers = CreateDrivers();
        _drivers.AddRange(Drivers);
        _engineerCandidates = new[]
        {
            new EngineerCandidate(StableId("engineer:mara-voss"), "Mara Voss", new Money(72_000_000), 88),
            new EngineerCandidate(StableId("engineer:elliot-shaw"), "Elliot Shaw", new Money(58_000_000), 79),
            new EngineerCandidate(StableId("engineer:nia-calder"), "Nia Calder", new Money(91_000_000), 94),
            new EngineerCandidate(StableId("engineer:tomas-vale"), "Tomas Vale", new Money(46_000_000), 73)
        };

        _commandBus = new CommandBus(_eventBus);
        _commandBus.Register(new HireEngineerHandler(PlayerTeam, _engineerCandidates, _clock));
        _eventBus.Subscribe<EngineerHiredEvent>(evt =>
            _inbox.Add($"{evt.OccurredOn:yyyy-MM-dd}: {GetEngineerName(evt.EngineerId)} joined {PlayerTeam.Name}."));
        _clock.DayAdvanced += OnDayAdvanced;
        _inbox.Add("Chief engineer: Start with a wind-tunnel study to identify a legal aerodynamic gain.");
        _inbox.Add("Race strategist: Five rounds are scheduled. Check the calendar and complete practice before qualifying.");
    }

    public Team PlayerTeam { get; }
    public IReadOnlyList<Team> Teams { get; }
    public IReadOnlyList<Driver> Drivers { get; }
    public IReadOnlyList<EngineerCandidate> EngineerCandidates => _engineerCandidates;
    public DateTime Today => _clock.Today;
    public IReadOnlyList<string> Inbox => _inbox;
    public IReadOnlyList<RaceCircuit> Circuits { get; }
    public IReadOnlyList<CalendarRaceEvent> Calendar => _calendar;
    public CalendarRaceEvent NextEvent => _calendar.FirstOrDefault(item => !item.Completed) ??
        new CalendarRaceEvent(Today.AddDays(35), Circuits[0]);
    public RaceWeekend Weekend { get; private set; }
    public TeamFacilities Facilities { get; }
    public CarDesign PlayerCarDesign => _carDesigns[PlayerTeam];
    public DevelopmentProject? CurrentDevelopment => _developmentProject;

    public CommandResult Execute(ICommand command) => _commandBus.Dispatch(command);

    public void AdvanceDay()
    {
        _clock.AdvanceDays(1);
    }

    public DashboardViewModel GetDashboard()
    {
        var pointsByTeam = Teams.Select(team => new
        {
            Team = team,
            Points = Drivers.Where(driver => GetTeamForDriver(driver) == team)
                .Sum(driver => _driverPoints.TryGetValue(GetDriverId(driver), out var points) ? points : 0)
        }).OrderByDescending(item => item.Points).ToList();
        var position = pointsByTeam.FindIndex(item => item.Team == PlayerTeam) + 1;

        return new DashboardViewModel
        {
            TeamName = PlayerTeam.Name,
            Today = Today,
            NextEvent = $"{NextEvent.Circuit.Name} ({NextEvent.Date:yyyy-MM-dd})",
            BudgetRemainingCents = PlayerTeam.Budget.Cents,
            ChampionshipPosition = position
        };
    }

    public IReadOnlyList<InboxItemViewModel> GetInbox() =>
        _inbox.Select(item => new InboxItemViewModel
        {
            Date = Today,
            Headline = item,
            Unread = true
        }).ToArray();

    public RaceSetup CreateRaceSetup(int laps, int seed)
    {
        if (laps < 3 || laps > 100)
            throw new ArgumentOutOfRangeException(nameof(laps), "Race length must be between 3 and 100 laps.");

        var grid = new List<CarSetup>();
        foreach (var teamIndex in Enumerable.Range(0, Teams.Count))
        {
            for (var driverIndex = 0; driverIndex < 2; driverIndex++)
            {
                var driver = Drivers[teamIndex * 2 + driverIndex];
                var team = Teams[teamIndex];
                grid.Add(new CarSetup
                {
                    CarId = StableId($"car:{team.Name}:{driver.Name}"),
                    TeamId = team.Id,
                    DriverId = GetDriverId(driver),
                    TeamName = team.Name,
                    DriverName = driver.Name,
                    CarNumber = 11 + teamIndex * 2 + driverIndex,
                    DriverPace = driver.Pace,
                    Consistency = driver.Consistency,
                    TyreManagement = driver.TyreManagement,
                    Racecraft = driver.Racecraft,
                    Focus = driver.Focus,
                    WetSkill = driver.WetSkill,
                    Aggression = driver.Aggression,
                    Defending = driver.Defending,
                    CarPaceFactor = TeamPaceFactor(teamIndex) +
                        (team == PlayerTeam ? PlayerCarDesign.PaceFactor - 1 -
                            (Weekend.PracticeComplete ? Weekend.RacePaceBonus : 0) : 0) +
                        driverIndex * 0.003,
                    StartingTyre = RaceTyreCompound.Medium,
                    FuelLoadKg = 110,
                    IsAi = team != PlayerTeam,
                    AiPitLap = team == PlayerTeam ? 0 : Math.Max(2, laps / 2)
                });
            }
        }

        if (Weekend.QualifyingComplete && Weekend.CircuitName == NextEvent.Circuit.Name)
        {
            var positions = Weekend.QualifyingResults.ToDictionary(item => item.CarNumber, item => item.GridPosition);
            grid = grid.OrderBy(car => positions.TryGetValue(car.CarNumber, out var position)
                ? position
                : int.MaxValue).ToList();
        }

        var circuit = NextEvent.Circuit;
        return new RaceSetup
        {
            TrackId = circuit.Id,
            TotalLaps = laps,
            Seed = seed,
            BaseLapTimeSeconds = circuit.BaseLapTimeSeconds,
            TyreWearFactor = circuit.TyreWearFactor,
            PitLossTimeSeconds = circuit.PitLossSeconds,
            OvertakingDifficulty = circuit.OvertakingDifficulty,
            Grid = grid
        };
    }

    public RaceWeekend StartWeekend()
    {
        if (Weekend.RaceComplete)
            Weekend = new RaceWeekend(NextEvent.Circuit.Name);
        return Weekend;
    }

    public IReadOnlyList<PracticeResult> RunPractice(int seed) =>
        RunPractice(seed, PracticeFocus.Setup);

    public IReadOnlyList<PracticeResult> RunPractice(int seed, PracticeFocus focus)
    {
        if (!Enum.IsDefined(typeof(PracticeFocus), focus))
            throw new ArgumentOutOfRangeException(nameof(focus));
        var circuit = NextEvent.Circuit;
        var random = new SeededRandom(seed);
        Weekend.PracticeFocus = focus;
        Weekend.SetupConfidence = focus == PracticeFocus.Setup
            ? Math.Min(100, 45 + Facilities.PracticeBonus + PlayerTeam.Engineers.Select(engineer => engineer.Skill).DefaultIfEmpty(0).Max() * 0.15)
            : focus == PracticeFocus.RaceSimulation ? 48 + Facilities.PracticeBonus : 42 + Facilities.PracticeBonus;
        Weekend.RacePaceBonus = focus == PracticeFocus.RaceSimulation
            ? 0.002 + Facilities.PracticeBonus / 10000.0
            : focus == PracticeFocus.Setup ? 0.001 + Weekend.SetupConfidence / 20000.0 : 0;
        var results = Drivers.Select(driver =>
        {
            var team = GetTeamForDriver(driver);
            var baseLap = circuit.BaseLapTimeSeconds *
                (TeamPaceFactor(GetTeamIndex(team)) + (team == PlayerTeam ? PlayerCarDesign.PaceFactor - 1 : 0)) *
                (1 - driver.Pace / 10000.0);
            var setupQuality = Facilities.PracticeBonus / 100.0;
            return new PracticeResult
            {
                DriverName = driver.Name,
                BestLapSeconds = baseLap - setupQuality +
                    (focus == PracticeFocus.Setup ? -0.12 : focus == PracticeFocus.TyreEvaluation ? 0.08 : 0) +
                    random.NextGaussian(0, 0.22),
                LongRunLapSeconds = baseLap * (1 + circuit.TyreWearFactor * 0.035) + random.NextGaussian(0, 0.3),
                TyreWearPerLap = circuit.TyreWearFactor * (1 - driver.TyreManagement / 400.0) *
                    (focus == PracticeFocus.TyreEvaluation ? 0.92 : 1),
                EngineerNote = focus switch
                {
                    PracticeFocus.Setup => $"Setup confidence {Weekend.SetupConfidence:F0}%; {driver.Name} reports a predictable balance.",
                    PracticeFocus.TyreEvaluation => $"Tyre data recommends a conservative stint; estimated wear {circuit.TyreWearFactor * (1 - driver.TyreManagement / 400.0) * 0.92:P1}/lap.",
                    _ => driver.Feedback >= 85
                        ? "Long-run simulation complete; race pace feedback is strong."
                        : "Long-run simulation complete; consider preserving tyres."
                }
            };
        }).OrderBy(result => result.BestLapSeconds).ToArray();
        Weekend.PracticeResults = results;
        Weekend.PracticeComplete = true;
        _inbox.Add($"{Today:yyyy-MM-dd}: Practice completed at {circuit.Name}; staff reviewed the long-run data.");
        return results;
    }

    public IReadOnlyList<QualifyingResult> RunQualifying(int seed) =>
        RunQualifying(seed, TyreCompoundType.Soft, QualiRisk.Normal, 2);

    public IReadOnlyList<QualifyingResult> RunQualifying(
        int seed,
        TyreCompoundType tyre,
        QualiRisk risk,
        int runs)
    {
        if (!Weekend.PracticeComplete)
            throw new InvalidOperationException("Complete practice before qualifying.");
        if (!Enum.IsDefined(typeof(TyreCompoundType), tyre))
            throw new ArgumentOutOfRangeException(nameof(tyre));
        if (!Enum.IsDefined(typeof(QualiRisk), risk))
            throw new ArgumentOutOfRangeException(nameof(risk));
        if (runs < 1 || runs > 5)
            throw new ArgumentOutOfRangeException(nameof(runs), "Qualifying runs must be between 1 and 5.");
        var circuit = NextEvent.Circuit;
        Weekend.QualifyingTyre = tyre;
        Weekend.QualifyingRisk = risk;
        Weekend.QualifyingRuns = runs;
        var random = new SeededRandom(seed);
        var results = Drivers.Select(driver =>
        {
            var team = GetTeamForDriver(driver);
            var playerDriver = team == PlayerTeam;
            var tyreFactor = playerDriver ? tyre switch
            {
                TyreCompoundType.Soft => 0.974,
                TyreCompoundType.Medium => 0.992,
                TyreCompoundType.Hard => 1.012,
                TyreCompoundType.Intermediate => 1.02,
                TyreCompoundType.Wet => 1.035,
                _ => 1
            } : 0.974;
            var riskFactor = playerDriver ? risk switch
            {
                QualiRisk.Push => 0.991,
                QualiRisk.Safe => 1.006,
                _ => 1
            } : 1;
            var runBonus = playerDriver ? (runs - 1) * 0.0012 : 0;
            var lapTime = circuit.BaseLapTimeSeconds *
                (TeamPaceFactor(GetTeamIndex(team)) + (team == PlayerTeam ? PlayerCarDesign.PaceFactor - 1 : 0)) *
                (1 - driver.Pace / 10000.0) - driver.Focus / 10000.0 +
                random.NextGaussian(0, (101 - driver.Consistency) *
                    (playerDriver && risk == QualiRisk.Push ? 0.009 : 0.006));
            lapTime *= tyreFactor * riskFactor * (1 - runBonus);
            if (playerDriver)
                lapTime *= 1 - Weekend.SetupConfidence / 10000.0;
            return new { Driver = driver, Time = lapTime };
        }).OrderBy(item => item.Time).ToArray();
        Weekend.QualifyingResults = results.Select((item, index) => new QualifyingResult
        {
            DriverName = item.Driver.Name,
            CarNumber = 11 + (_drivers.IndexOf(item.Driver) / 2) * 2 + _drivers.IndexOf(item.Driver) % 2,
            LapTimeSeconds = item.Time,
            GridPosition = index + 1
        }).ToArray();
        Weekend.QualifyingComplete = true;
        _inbox.Add($"{Today:yyyy-MM-dd}: Qualifying complete at {circuit.Name}; grid positions are confirmed.");
        return Weekend.QualifyingResults;
    }

    public RaceStrategyAdvice GetTyreAdvice(RaceSnapshot snapshot, int carNumber)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));
        var car = snapshot.Cars.FirstOrDefault(item => item.CarNumber == carNumber);
        if (car is null || car.TeamId != PlayerTeam.Id)
            throw new ArgumentException("Tyre advice is available for your team's cars only.", nameof(carNumber));
        var driver = Drivers.First(item => item.Name == car.DriverName);
        var measuredWearRate = Weekend.PracticeFocus == PracticeFocus.TyreEvaluation
            ? Weekend.PracticeResults.FirstOrDefault(item => item.DriverName == driver.Name)?.TyreWearPerLap
            : null;
        var wearRate = measuredWearRate ??
            NextEvent.Circuit.TyreWearFactor * (1 - driver.TyreManagement / 400.0);
        var lapsRemaining = Math.Max(0, NextEvent.Circuit.RaceLaps - car.LapsCompleted);
        var predictedLife = (1 - car.TyreWear) / Math.Max(0.01, wearRate * 0.09);
        var engineer = PlayerTeam.Engineers.OrderByDescending(item => item.Skill).FirstOrDefault();
        var staffName = engineer?.Name ?? "Race engineer";
        var confidence = engineer?.Skill ?? 62;
        var pitWearThreshold = 0.70 - confidence / 1000.0;
        var recommendPit = car.TyreWear >= pitWearThreshold ||
            (lapsRemaining > 2 && predictedLife < Math.Min(5, lapsRemaining));
        var suggestedTyre = lapsRemaining > 6 ? RaceTyreCompound.Hard : RaceTyreCompound.Soft;
        if (suggestedTyre == car.Tyre)
            suggestedTyre = RaceTyreCompound.Medium;
        var driverAdvice = car.TyreWear >= pitWearThreshold
            ? $"{driver.Name}: tyres are fading; box soon."
            : $"{driver.Name}: tyre grip is holding for now.";
        var staffAdvice = recommendPit
            ? $"{staffName} ({confidence}): box for {suggestedTyre}; estimated life is running short."
            : $"{staffName} ({confidence}): stay out; estimated tyre life is adequate.";
        return new RaceStrategyAdvice
        {
            Message = $"{staffAdvice} {driverAdvice}",
            SuggestedTyre = suggestedTyre,
            RecommendPit = recommendPit
        };
    }

    public DevelopmentStageResult StartDevelopment(DevelopmentFocus focus, Money budget)
    {
        if (_developmentProject is not null &&
            _developmentProject.Stage is not (DevelopmentStage.Complete or DevelopmentStage.Rejected))
            throw new InvalidOperationException("Finish or abandon the active development project first.");
        if (budget.Cents < 10_000_000)
            throw new ArgumentOutOfRangeException(nameof(budget), "Development needs at least $100,000.");
        PlayerTeam.Deduct(budget);
        _developmentProject = new DevelopmentProject(focus, budget, $"{focus} upgrade");
        return AdvanceDevelopment();
    }

    public DevelopmentStageResult AdvanceDevelopment()
    {
        var project = _developmentProject ??
            throw new InvalidOperationException("No active development project.");
        if (project.Stage is DevelopmentStage.Complete or DevelopmentStage.Rejected)
            throw new InvalidOperationException("The development project has already finished.");
        project.Progress += 1;
        if (project.Progress >= 3)
        {
            project.Progress = 0;
            project.Stage = project.Stage switch
            {
                DevelopmentStage.WindTunnel => DevelopmentStage.Design,
                DevelopmentStage.Design => DevelopmentStage.Build,
                DevelopmentStage.Build => DevelopmentStage.Complete,
                _ => project.Stage
            };
            if (project.Stage == DevelopmentStage.Complete)
            {
                var bonus = Facilities.ResearchBonus + Facilities.BuildQualityBonus;
                project.DownforceDelta = project.Focus == DevelopmentFocus.Aerodynamics ? 7 + bonus * 0.1 : 1;
                project.DragDelta = project.Focus == DevelopmentFocus.Aerodynamics ? 4 + bonus * 0.08 : 1;
                project.ReliabilityDelta = project.Focus == DevelopmentFocus.Reliability ? 8 + bonus * 0.15 : -2;
                project.LifespanDeltaLaps = project.Focus == DevelopmentFocus.Reliability ? 80 + bonus : -20;
                var capAddition = project.Budget.Cents;
                if (PlayerCarDesign.CostCapSpentCents + capAddition > CarDesign.CostCapLimitCents)
                {
                    project.Stage = DevelopmentStage.Rejected;
                    _inbox.Add("Regulatory office: The upgrade exceeds the cost cap and cannot be fitted.");
                    return new DevelopmentStageResult { Stage = project.Stage, Failed = true, Message = "Rejected: cost cap exceeded." };
                }
                PlayerCarDesign.Downforce = Math.Min(100, PlayerCarDesign.Downforce + project.DownforceDelta);
                PlayerCarDesign.DragEfficiency = Math.Min(100, PlayerCarDesign.DragEfficiency + project.DragDelta);
                PlayerCarDesign.Reliability = Math.Clamp(PlayerCarDesign.Reliability + project.ReliabilityDelta, 0, 100);
                PlayerCarDesign.ComponentLifeLaps = Math.Max(100, PlayerCarDesign.ComponentLifeLaps + project.LifespanDeltaLaps);
                PlayerCarDesign.CostCapSpentCents += capAddition;
                _inbox.Add($"Chief engineer: {project.Focus} package completed; lifespan and regulatory checks passed.");
                return new DevelopmentStageResult { Stage = project.Stage, Complete = true, Message = "Upgrade complete and legal." };
            }
        }
        return new DevelopmentStageResult
        {
            Stage = project.Stage,
            Message = $"{project.Stage}: work step {project.Progress}/3 complete."
        };
    }

    public void UpgradeFacility(string facility)
    {
        var cost = Facilities.UpgradeCost(facility);
        PlayerTeam.Deduct(cost);
        Facilities.Upgrade(facility);
        _inbox.Add($"Facilities manager: {facility} upgraded for {FormatMoney(cost)}.");
    }

    public void CompleteNextRace()
    {
        var next = NextEvent;
        next.Completed = true;
        Weekend.RaceComplete = true;
        if (_calendar.All(item => item.Completed))
            _inbox.Add("Season review: all scheduled races are complete.");
        else
            Weekend = new RaceWeekend(NextEvent.Circuit.Name);
    }

    public IReadOnlyList<TeamRacePayout> SettleRace(RaceSetup setup, RaceResult result)
    {
        var byId = setup.Grid.ToDictionary(car => car.CarId);
        var payouts = new Dictionary<EntityId, long>();
        var driverIds = new Dictionary<EntityId, EntityId>();
        for (var index = 0; index < result.Classification.Count; index++)
        {
            var car = byId[result.Classification[index]];
            var points = index < PointsByPosition.Length ? PointsByPosition[index] : 0;
            var prize = index < PrizeCentsByPosition.Length ? PrizeCentsByPosition[index] : 0;
            _teamsById[car.TeamId].AddPoints(points);
            _driverPoints[car.DriverId] = _driverPoints.TryGetValue(car.DriverId, out var current)
                ? current + points
                : points;
            payouts[car.TeamId] = payouts.TryGetValue(car.TeamId, out var currentPrize)
                ? checked(currentPrize + prize)
                : prize;
            driverIds[car.TeamId] = car.DriverId;
        }

        var settlement = payouts.Select(pair =>
        {
            var amount = new Money(pair.Value);
            _teamsById[pair.Key].AddFunds(amount);
            return new TeamRacePayout(_teamsById[pair.Key].Name, amount, pair.Key == PlayerTeam.Id);
        }).OrderByDescending(item => item.Amount.Cents).ToArray();
        var playerPayout = settlement.FirstOrDefault(item => item.IsPlayer);
        if (playerPayout is not null)
            _inbox.Add($"Race prize paid: {FormatMoney(playerPayout.Amount)} for {PlayerTeam.Name}.");
        return settlement;
    }

    public static string FormatMoney(Money money) =>
        (money.Cents / 100m).ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

    private void OnDayAdvanced(DateTime date)
    {
        if (date.Day != 1)
            return;

        foreach (var team in Teams)
        {
            var payroll = team.MonthlyPayroll;
            if (payroll.Cents > team.Budget.Cents)
            {
                _inbox.Add($"{date:yyyy-MM-dd}: {team.Name} cannot cover payroll; team is insolvent.");
                continue;
            }

            team.Deduct(payroll);
            _eventBus.Publish(new PayrollPaidEvent(date, team.Id, payroll));
        }
        _inbox.Add($"{date:yyyy-MM-dd}: monthly payroll processed.");
    }

    private string GetEngineerName(EntityId id) =>
        _engineerCandidates.First(candidate => candidate.Id == id).Name;

    private Team GetTeamForDriver(Driver driver) =>
        Teams[_drivers.IndexOf(driver) / 2];

    private int GetTeamIndex(Team team)
    {
        for (var index = 0; index < Teams.Count; index++)
        {
            if (Teams[index] == team)
                return index;
        }
        throw new InvalidOperationException("The team is not part of this championship.");
    }

    private EntityId GetDriverId(Driver driver) => _driverIds[driver];

    private readonly Dictionary<Driver, EntityId> _driverIds = new();

    private List<Driver> CreateDrivers()
    {
        var definitions = new[]
        {
            ("Lena Hart", 91, 89, 87), ("Marco Bell", 86, 84, 82),
            ("Ivo Marin", 90, 85, 81), ("Sofia Rojas", 84, 88, 90),
            ("Callum Reed", 87, 83, 85), ("Yuki Tanaka", 85, 91, 88),
            ("Amara Okafor", 83, 86, 84), ("Felix Brandt", 89, 80, 78),
            ("Noor Haddad", 86, 87, 86), ("Rafael Costa", 82, 90, 89)
        };
        var drivers = new List<Driver>(definitions.Length);
        foreach (var (name, pace, consistency, tyreManagement) in definitions)
        {
            var driver = new Driver
            {
                Name = name,
                Pace = pace,
                Consistency = consistency,
                TyreManagement = tyreManagement,
                WetSkill = consistency,
                Racecraft = Math.Min(98, (pace + consistency) / 2),
                Focus = Math.Min(98, consistency + 3),
                Experience = Math.Max(35, consistency - 8),
                Feedback = Math.Min(97, (consistency + tyreManagement) / 2),
                Aggression = Math.Min(96, pace - 3),
                Defending = Math.Min(96, (pace + consistency) / 2)
            };
            _driverIds.Add(driver, StableId($"driver:{name}"));
            drivers.Add(driver);
        }
        return drivers;
    }

    private static double TeamPaceFactor(int teamIndex) => teamIndex switch
    {
        0 => 0.992,
        1 => 0.982,
        2 => 0.998,
        3 => 0.987,
        _ => 1.004
    };

    private static EntityId StableId(string key)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, guidBytes.Length);
        return new EntityId(new Guid(guidBytes));
    }
}

public sealed class TeamRacePayout
{
    public TeamRacePayout(string teamName, Money amount, bool isPlayer)
    {
        TeamName = teamName;
        Amount = amount;
        IsPlayer = isPlayer;
    }

    public string TeamName { get; }
    public Money Amount { get; }
    public bool IsPlayer { get; }
}
