using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GrandPrix.AI;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Commands;
using GrandPrix.Core.Common;
using GrandPrix.Core.Events;
using GrandPrix.Core.Simulation;
using GrandPrix.Data;
using GrandPrix.Domain.Calendar;
using GrandPrix.Domain.Car;
using GrandPrix.Domain.Drivers;
using GrandPrix.Domain.Facilities;
using GrandPrix.Domain.Race;
using GrandPrix.Domain.Staff;
using GrandPrix.Domain.Team;
using GrandPrix.Series.Abstractions;
using GrandPrix.Series.F1;

namespace GrandPrix.Application;

/// <summary>Coordinates the management, race-weekend, and championship state for one game.</summary>
public sealed class GameSession : IGameSession
{
    private readonly ISeriesRulesProvider _seriesRules;
    private readonly ITeamPrincipalPolicy _teamPrincipalPolicy;
    private readonly IAiRaceStrategyPolicy _aiRaceStrategyPolicy;
    private readonly int _contentSchemaVersion;
    private readonly InMemoryEventBus _eventBus = new();
    private readonly CommandBus _commandBus;
    private readonly ManualGameClock _clock;
    private readonly List<string> _inbox = new();
    private readonly List<DateTime> _inboxDates = new();
    private readonly List<SessionCommandRecord> _commandHistory = new();
    private readonly Dictionary<EntityId, int> _driverPoints = new();
    private readonly List<Driver> _drivers = new();
    private readonly Dictionary<EntityId, Team> _teamsById = new();
    private readonly Dictionary<EntityId, double> _teamPaceFactors = new();
    private readonly IReadOnlyList<EngineerCandidate> _engineerCandidates;
    private readonly List<CalendarRaceEvent> _calendar;
    private readonly Dictionary<Team, CarDesign> _carDesigns = new();
    private DevelopmentProject? _developmentProject;
    private IRaceEngine? _activeRaceEngine;
    private RaceSetup? _activeRaceSetup;
    private readonly List<RaceEvent> _activeRaceEvents = new();
    private bool _liveRaceSettled;
    private bool _isRestoring;
    private int _readInboxCount;
    private AdvanceSummaryViewModel? _lastAdvanceSummary;
    private RaceSettlementViewModel? _lastRaceSettlement;

    public GameSession() : this(
        GameContentLoader.LoadEmbedded(),
        new F1RulesProvider(),
        new ConservativeTeamPrincipalPolicy(),
        new ConservativeAiRaceStrategyPolicy())
    {
    }

    internal GameSession(
        GameContentDefinition content,
        ISeriesRulesProvider seriesRules,
        ITeamPrincipalPolicy teamPrincipalPolicy,
        IAiRaceStrategyPolicy aiRaceStrategyPolicy)
    {
        if (content is null)
            throw new ArgumentNullException(nameof(content));
        _seriesRules = seriesRules ?? throw new ArgumentNullException(nameof(seriesRules));
        _teamPrincipalPolicy = teamPrincipalPolicy ??
            throw new ArgumentNullException(nameof(teamPrincipalPolicy));
        _aiRaceStrategyPolicy = aiRaceStrategyPolicy ??
            throw new ArgumentNullException(nameof(aiRaceStrategyPolicy));
        var validationErrors = GameContentLoader.Validate(content);
        if (validationErrors.Count > 0)
            throw new GameContentException(validationErrors);
        _contentSchemaVersion = content.SchemaVersion;

        _clock = new ManualGameClock(content.StartDate);
        var teamsByContentId = content.Teams.ToDictionary(
            definition => definition.Id,
            definition => new Team(
                StableId($"team:{definition.Id}"),
                definition.Name,
                new Money(definition.BudgetCents),
                new Money(definition.MonthlyBasePayrollCents)));
        Teams = content.Teams.Select(definition => teamsByContentId[definition.Id]).ToArray();
        PlayerTeam = teamsByContentId[content.PlayerTeamId];
        foreach (var definition in content.Teams)
        {
            var team = teamsByContentId[definition.Id];
            _teamsById.Add(team.Id, team);
            _teamPaceFactors.Add(team.Id, definition.CarPaceFactor);
        }
        foreach (var team in Teams)
            _carDesigns.Add(team, new CarDesign());
        Facilities = new TeamFacilities();
        Circuits = content.Circuits.Select(definition => new RaceCircuit(
            definition.Id,
            definition.Name,
            definition.Country,
            definition.RaceLaps,
            definition.BaseLapTimeSeconds,
            definition.TyreWearFactor,
            definition.PitLossSeconds,
            definition.OvertakingDifficulty,
            definition.Characteristic)).ToArray();
        var circuitsById = Circuits.ToDictionary(circuit => circuit.Id);
        if (_seriesRules.Calendar.Count == 0)
            throw new InvalidOperationException("The selected series must define at least one calendar round.");
        if (_seriesRules.CostCapLimitCents <= 0)
            throw new InvalidOperationException("The selected series must define a positive cost cap.");
        var previousRoundDay = 0;
        _calendar = _seriesRules.Calendar.Select(round =>
        {
            if (!circuitsById.TryGetValue(round.CircuitId, out var circuit))
                throw new InvalidOperationException(
                    $"Series '{_seriesRules.SeriesId}' references unknown circuit '{round.CircuitId}'.");
            if (round.DaysAfterSeasonStart <= previousRoundDay)
                throw new InvalidOperationException(
                    $"Series '{_seriesRules.SeriesId}' calendar offsets must be strictly increasing.");
            previousRoundDay = round.DaysAfterSeasonStart;
            return new CalendarRaceEvent(_clock.Today.AddDays(round.DaysAfterSeasonStart), circuit);
        }).ToList();
        Weekend = new RaceWeekend(NextEvent.Circuit.Name);

        var driverDefinitions = content.Teams
            .SelectMany(team => content.Drivers.Where(driver => driver.TeamId == team.Id))
            .ToArray();
        Drivers = driverDefinitions.Select(definition => new Driver
        {
            Name = definition.Name,
            Pace = definition.Pace,
            Consistency = definition.Consistency,
            TyreManagement = definition.TyreManagement,
            WetSkill = definition.WetSkill,
            Racecraft = definition.Racecraft,
            Focus = definition.Focus,
            Experience = definition.Experience,
            Feedback = definition.Feedback,
            Aggression = definition.Aggression,
            Defending = definition.Defending
        }).ToArray();
        _drivers.AddRange(Drivers);
        for (var index = 0; index < Drivers.Count; index++)
            _driverIds.Add(Drivers[index], StableId($"driver:{driverDefinitions[index].Name}"));
        _engineerCandidates = content.Engineers.Select(definition =>
            new EngineerCandidate(
                StableId($"engineer:{definition.Id}"),
                definition.Name,
                new Money(definition.AskingAnnualSalaryCents),
                definition.Skill)).ToArray();

        _commandBus = new CommandBus(_eventBus);
        var engineerHandlers = Teams.ToDictionary(
            team => team.Id,
            team => new HireEngineerHandler(team, _engineerCandidates, _clock));
        _commandBus.Register(new DelegateCommandHandler<HireEngineerCommand>(command =>
            engineerHandlers.TryGetValue(command.Issuer, out var handler)
                ? handler.Handle(command)
                : CommandResult.Fail("The issuing team is not part of this championship.")));
        _commandBus.Register(new DelegateCommandHandler<UpgradeFacilityAction>(command =>
            HandleAction("facility upgraded", () =>
            {
                UpgradeFacility(command.Facility);
                return $"{command.Facility} upgraded.";
            })));
        _commandBus.Register(new DelegateCommandHandler<StartDevelopmentAction>(HandleStartDevelopment));
        _commandBus.Register(new DelegateCommandHandler<AdvanceDevelopmentAction>(_ =>
            HandleAction("development advanced", () => AdvanceDevelopment().Message)));
        _commandBus.Register(new DelegateCommandHandler<AdvanceCalendarAction>(HandleAdvanceCalendar));
        _commandBus.Register(new DelegateCommandHandler<RunPracticeAction>(HandleRunPractice));
        _commandBus.Register(new DelegateCommandHandler<RunQualifyingAction>(HandleRunQualifying));
        _commandBus.Register(new DelegateCommandHandler<StartRaceAction>(command =>
            HandleAction("race started", () =>
            {
                StartRaceCore(command.Laps, command.Seed);
                return $"Race started with seed {command.Seed}.";
            })));
        _commandBus.Register(new DelegateCommandHandler<QueuePitStopAction>(HandleQueuePitStop));
        _commandBus.Register(new DelegateCommandHandler<SetRaceModeAction>(HandleSetRaceMode));
        _commandBus.Register(new DelegateCommandHandler<SettleLiveRaceAction>(_ =>
            HandleAction("race settled", () =>
            {
                _lastRaceSettlement = SettleLiveRaceCore();
                return "Race settlement completed.";
            })));
        _commandBus.Register(new DelegateCommandHandler<AdvanceLiveRaceAction>(command =>
            HandleAction("race advanced", () =>
            {
                AdvanceRaceCore(command.ElapsedSeconds);
                return $"Race advanced by {command.ElapsedSeconds:0.###} seconds.";
            })));
        _commandBus.Register(new DelegateCommandHandler<MarkInboxReadAction>(_ =>
            HandleAction("inbox read", () =>
            {
                _readInboxCount = _inbox.Count;
                return "Inbox marked as read.";
            })));
        _eventBus.Subscribe<EngineerHiredEvent>(evt =>
            AddInboxMessage(
                $"{evt.OccurredOn:yyyy-MM-dd}: {GetEngineerName(evt.EngineerId)} joined {GetTeamName(evt.TeamId)}.",
                evt.OccurredOn));
        _clock.DayAdvanced += OnDayAdvanced;
        AddInboxMessage("Chief engineer: Start with a wind-tunnel study to identify a legal aerodynamic gain.");
        AddInboxMessage("Race strategist: Five rounds are scheduled. Check the calendar and complete practice before qualifying.");
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

    public CommandResult Execute(ICommand command)
    {
        var result = _commandBus.Dispatch(command);
        if (result.Succeeded && !_isRestoring)
            _commandHistory.Add(ToCommandRecord(command));
        return result;
    }

    internal GameSessionSaveData CaptureSaveData() => new()
    {
        ContentSchemaVersion = _contentSchemaVersion,
        SeriesId = _seriesRules.SeriesId,
        RaceStrategyPolicyId = _aiRaceStrategyPolicy.PolicyId,
        Commands = _commandHistory.Select(CloneCommandRecord).ToList()
    };

    internal void RestoreSaveData(GameSessionSaveData saveData)
    {
        if (saveData is null)
            throw new ArgumentNullException(nameof(saveData));
        if (saveData.Commands is null)
            throw new InvalidDataException("Save data does not contain a command history.");
        if (saveData.ContentSchemaVersion != _contentSchemaVersion)
            throw new InvalidDataException(
                $"Save content schema {saveData.ContentSchemaVersion} does not match " +
                $"the current schema {_contentSchemaVersion}.");
        if (!string.Equals(saveData.SeriesId, _seriesRules.SeriesId, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Save series '{saveData.SeriesId}' does not match the current series '{_seriesRules.SeriesId}'.");
        if (!string.Equals(
                saveData.RaceStrategyPolicyId,
                _aiRaceStrategyPolicy.PolicyId,
                StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Save race strategy '{saveData.RaceStrategyPolicyId}' does not match the current policy.");

        _isRestoring = true;
        try
        {
            for (var index = 0; index < saveData.Commands.Count; index++)
            {
                ICommand command;
                try
                {
                    command = FromCommandRecord(saveData.Commands[index]);
                }
                catch (FormatException exception)
                {
                    throw new InvalidDataException(
                        $"Save command {index + 1} contains an invalid value.",
                        exception);
                }
                var result = _commandBus.Dispatch(command);
                if (!result.Succeeded)
                    throw new InvalidDataException(
                        $"Save command {index + 1} could not be replayed: {result.Error}");
            }

            _commandHistory.Clear();
            _commandHistory.AddRange(saveData.Commands.Select(CloneCommandRecord));
        }
        finally
        {
            _isRestoring = false;
        }
    }

    public CommandResult HireEngineer(int marketNumber, long offeredAnnualSalaryCents, int contractYears)
    {
        if (marketNumber < 1 || marketNumber > _engineerCandidates.Count)
            return CommandResult.Fail("Choose an engineer from the displayed market.");

        var candidate = _engineerCandidates[marketNumber - 1];
        return Execute(new HireEngineerCommand(
            PlayerTeam.Id,
            candidate.Id,
            new Money(offeredAnnualSalaryCents),
            contractYears));
    }

    public void AdvanceDay()
    {
        var result = AdvanceDays(1);
        if (result.DaysAdvanced != 1)
            throw new InvalidOperationException(result.Message);
    }

    public AdvanceSummaryViewModel AdvanceDays(int days)
    {
        if (days < 1 || days > 3650)
            throw new ArgumentOutOfRangeException(nameof(days), "Advance between 1 and 3650 days.");

        DispatchOrThrow(new AdvanceCalendarAction(PlayerTeam.Id, days, toNextEvent: false));
        RunRivalPrincipalTurns();
        return _lastAdvanceSummary ??
            throw new InvalidOperationException("Calendar advancement completed without a summary.");
    }

    private AdvanceSummaryViewModel AdvanceDaysCore(int days)
    {
        var startDate = Today;
        var startInboxCount = _inbox.Count;
        var advanced = 0;
        while (advanced < days)
        {
            _clock.AdvanceDays(1);
            advanced++;
            if (HasPayrollIssueSince(startInboxCount))
                break;
        }

        return new AdvanceSummaryViewModel
        {
            DaysAdvanced = advanced,
            Today = Today,
            ReachedNextEvent = Today >= NextEvent.Date,
            StoppedForAttention = HasPayrollIssueSince(startInboxCount),
            Message = GetAdvanceMessage(startDate, advanced, HasPayrollIssueSince(startInboxCount))
        };
    }

    public AdvanceSummaryViewModel AdvanceToNextEvent()
    {
        DispatchOrThrow(new AdvanceCalendarAction(PlayerTeam.Id, days: null, toNextEvent: true));
        RunRivalPrincipalTurns();
        return _lastAdvanceSummary ??
            throw new InvalidOperationException("Calendar advancement completed without a summary.");
    }

    private AdvanceSummaryViewModel AdvanceToNextEventCore()
    {
        var startDate = Today;
        var startInboxCount = _inbox.Count;
        var advanced = 0;
        while (Today < NextEvent.Date && advanced < 3650)
        {
            _clock.AdvanceDays(1);
            advanced++;
            if (HasPayrollIssueSince(startInboxCount))
                break;
        }

        var reachedNextEvent = Today >= NextEvent.Date;
        var stoppedForAttention = HasPayrollIssueSince(startInboxCount);
        return new AdvanceSummaryViewModel
        {
            DaysAdvanced = advanced,
            Today = Today,
            ReachedNextEvent = reachedNextEvent,
            StoppedForAttention = stoppedForAttention,
            Message = reachedNextEvent
                ? $"Arrived at {NextEvent.Circuit.Name} race week."
                : GetAdvanceMessage(startDate, advanced, stoppedForAttention)
        };
    }

    public DashboardViewModel GetDashboard()
    {
        var standings = GetStandings();
        var position = standings.ToList().FindIndex(item => item.IsPlayer) + 1;
        var nextEvent = NextEvent;
        var design = PlayerCarDesign;

        return new DashboardViewModel
        {
            TeamName = PlayerTeam.Name,
            Today = Today,
            NextEvent = $"{nextEvent.Circuit.Name} ({nextEvent.Date:yyyy-MM-dd})",
            NextCircuit = nextEvent.Circuit.Name,
            NextEventDate = nextEvent.Date,
            RoundsRemaining = _calendar.Count(item => !item.Completed),
            BudgetRemainingCents = PlayerTeam.Budget.Cents,
            ChampionshipPosition = position,
            ChampionshipPoints = PlayerTeam.ChampionshipPoints,
            CostCapSpentCents = design.CostCapSpentCents,
            CostCapHeadroomCents = Math.Max(0, _seriesRules.CostCapLimitCents - design.CostCapSpentCents),
            Downforce = design.Downforce,
            DragEfficiency = design.DragEfficiency,
            Reliability = design.Reliability,
            ComponentLifeLaps = design.ComponentLifeLaps,
            StaffCount = PlayerTeam.Engineers.Count,
            UnreadInboxCount = Math.Max(0, _inbox.Count - _readInboxCount),
            LatestInboxHeadlines = _inbox.TakeLast(3).Reverse().ToArray()
        };
    }

    public IReadOnlyList<InboxItemViewModel> GetInbox() =>
        _inbox.Select((item, index) => new InboxItemViewModel
        {
            Date = _inboxDates[index],
            Headline = item,
            Unread = index >= _readInboxCount
        }).ToArray();

    public void MarkInboxRead() =>
        DispatchOrThrow(new MarkInboxReadAction(PlayerTeam.Id));

    public IReadOnlyList<DriverViewModel> GetDrivers() =>
        Drivers.Select((driver, index) => new DriverViewModel
        {
            Name = driver.Name,
            TeamName = Teams[index / 2].Name,
            CarNumber = 11 + index,
            Pace = driver.Pace,
            Racecraft = driver.Racecraft,
            Focus = driver.Focus,
            Consistency = driver.Consistency,
            TyreManagement = driver.TyreManagement,
            WetSkill = driver.WetSkill,
            Experience = driver.Experience,
            Feedback = driver.Feedback,
            Aggression = driver.Aggression,
            Defending = driver.Defending
        }).ToArray();

    public RaceWeekendViewModel GetRaceWeekend()
    {
        var weekend = Weekend;
        var eventView = _calendar.FirstOrDefault(item => !item.Completed);
        return new RaceWeekendViewModel
        {
            Circuit = eventView?.Circuit.Name ?? string.Empty,
            Country = eventView?.Circuit.Country ?? string.Empty,
            RaceLaps = eventView?.Circuit.RaceLaps ?? 0,
            PracticeComplete = weekend.PracticeComplete,
            QualifyingComplete = weekend.QualifyingComplete,
            RaceComplete = weekend.RaceComplete
        };
    }

    public IReadOnlyList<PracticeResultViewModel> RunPracticeView(int seed, string focus)
    {
        DispatchOrThrow(new RunPracticeAction(PlayerTeam.Id, seed, focus));
        return Weekend.PracticeResults.Select(result => new PracticeResultViewModel
        {
            DriverName = result.DriverName,
            BestLapSeconds = result.BestLapSeconds,
            LongRunLapSeconds = result.LongRunLapSeconds,
            TyreWearPerLap = result.TyreWearPerLap,
            EngineerNote = result.EngineerNote
        }).ToArray();
    }

    public IReadOnlyList<QualifyingResultViewModel> RunQualifyingView(
        int seed,
        string tyre,
        string risk,
        int runs)
    {
        DispatchOrThrow(new RunQualifyingAction(PlayerTeam.Id, seed, tyre, risk, runs));
        return Weekend.QualifyingResults.Select(result => new QualifyingResultViewModel
        {
            DriverName = result.DriverName,
            CarNumber = result.CarNumber,
            GridPosition = result.GridPosition,
            LapTimeSeconds = result.LapTimeSeconds
        }).ToArray();
    }

    public ActionResultViewModel UpgradeFacilityView(string facility)
    {
        var result = Execute(new UpgradeFacilityAction(PlayerTeam.Id, facility));
        return ToActionResult(result);
    }

    public ActionResultViewModel StartDevelopmentView(string focus, long budgetCents)
    {
        return ToActionResult(Execute(new StartDevelopmentAction(PlayerTeam.Id, focus, budgetCents)));
    }

    public ActionResultViewModel AdvanceDevelopmentView()
    {
        return ToActionResult(Execute(new AdvanceDevelopmentAction(PlayerTeam.Id)));
    }

    public RaceTimingViewModel StartRace(int laps, int seed)
    {
        DispatchOrThrow(new StartRaceAction(PlayerTeam.Id, laps, seed));
        return GetRaceTiming();
    }

    private RaceTimingViewModel StartRaceCore(int laps, int seed)
    {
        if (_activeRaceEngine is not null && !_liveRaceSettled)
            throw new InvalidOperationException("Settle or finish the current race before starting another.");
        if (!Weekend.QualifyingComplete)
            throw new InvalidOperationException("Complete qualifying before starting the race.");

        _activeRaceSetup = CreateRaceSetup(laps, seed);
        _activeRaceEngine = new LapRaceEngine();
        _activeRaceEngine.Start(_activeRaceSetup, new SeededRandom(seed).Fork("race"));
        _activeRaceEvents.Clear();
        _liveRaceSettled = false;
        SubmitAiRaceCommands();
        return GetRaceTiming();
    }

    public RaceTimingViewModel AdvanceRace(double elapsedSeconds)
    {
        DispatchOrThrow(new AdvanceLiveRaceAction(PlayerTeam.Id, elapsedSeconds));
        return GetRaceTiming();
    }

    private void AdvanceRaceCore(double elapsedSeconds)
    {
        if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) ||
            elapsedSeconds <= 0 || elapsedSeconds > 60)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Advance a race by more than 0 and at most 60 seconds.");
        var engine = GetActiveRaceEngine();
        _activeRaceEvents.AddRange(engine.Advance(TimeSpan.FromSeconds(elapsedSeconds)));
        SubmitAiRaceCommands();
    }

    public RaceTimingViewModel GetRaceTiming()
    {
        var engine = GetActiveRaceEngine();
        var setup = _activeRaceSetup ??
            throw new InvalidOperationException("There is no active race setup.");
        var snapshot = engine.GetSnapshot();
        var stopsByCar = _activeRaceEvents.OfType<PitStopEvent>()
            .GroupBy(item => item.CarId)
            .ToDictionary(group => group.Key, group => group.Count());

        return new RaceTimingViewModel
        {
            CurrentLap = snapshot.CurrentLap,
            TotalLaps = setup.TotalLaps,
            RaceTime = snapshot.RaceTime,
            SafetyCarActive = snapshot.SafetyCarActive,
            RainIntensity = snapshot.RainIntensity,
            Finished = engine.IsFinished,
            Cars = snapshot.Cars.OrderBy(car => car.Position).Select(car =>
            {
                var stops = stopsByCar.TryGetValue(car.CarId, out var count) ? count : 0;
                var isPlayer = car.TeamId == PlayerTeam.Id;
                return new RaceCarViewModel
                {
                    CarNumber = car.CarNumber,
                    DriverName = car.DriverName,
                    TeamName = car.TeamName,
                    Position = car.Position,
                    LapsCompleted = car.LapsCompleted,
                    GapToLeaderSeconds = car.GapToLeaderSec,
                    GapToAheadSeconds = car.GapToAheadSec,
                    Tyre = car.Tyre.ToString(),
                    TyreWear = car.TyreWear,
                    PitStops = stops,
                    Mode = car.Mode.ToString(),
                    Status = car.Retired ? "OUT" : car.InPit ? "BOX" :
                        snapshot.SafetyCarActive ? "SC" : stops > 0 ? "PIT" : string.Empty,
                    IsPlayerCar = isPlayer
                };
            }).ToArray(),
            RecentEvents = _activeRaceEvents.TakeLast(6)
                .Select(item => item.Description)
                .Where(description => !string.IsNullOrWhiteSpace(description))
                .ToArray()
        };
    }

    public RaceActionViewModel QueuePitStop(int carNumber, string tyre, double extraDelaySeconds)
    {
        var result = Execute(new QueuePitStopAction(PlayerTeam.Id, carNumber, tyre, extraDelaySeconds));
        return ToRaceActionResult(result);
    }

    private RaceActionViewModel QueuePitStopCore(int carNumber, string tyre, double extraDelaySeconds)
    {
        if (!Enum.TryParse(tyre, true, out RaceTyreCompound parsedTyre) ||
            !Enum.IsDefined(typeof(RaceTyreCompound), parsedTyre))
            return new RaceActionViewModel { Message = "Choose soft, medium, hard, intermediate, or wet tyres." };
        if (double.IsNaN(extraDelaySeconds) || double.IsInfinity(extraDelaySeconds) ||
            extraDelaySeconds < 0 || extraDelaySeconds > 300)
            return new RaceActionViewModel { Message = "Pit delay must be between 0 and 300 seconds." };

        var engine = GetActiveRaceEngine();
        var car = engine.GetSnapshot().Cars.FirstOrDefault(item =>
            item.CarNumber == carNumber && item.TeamId == PlayerTeam.Id);
        if (car is null || car.Retired)
            return new RaceActionViewModel { Message = $"Car #{carNumber} is not available for a pit instruction." };

        engine.Submit(new BoxThisLapCommand(car.CarId, parsedTyre, extraDelaySeconds));
        return new RaceActionViewModel
        {
            Succeeded = true,
            Message = $"Car #{carNumber} will box this lap for {parsedTyre} tyres."
        };
    }

    public RaceActionViewModel SetRaceMode(int carNumber, string mode)
    {
        var result = Execute(new SetRaceModeAction(PlayerTeam.Id, carNumber, mode));
        return ToRaceActionResult(result);
    }

    private RaceActionViewModel SetRaceModeCore(int carNumber, string mode)
    {
        if (!Enum.TryParse(mode, true, out DriverMode parsedMode) ||
            !Enum.IsDefined(typeof(DriverMode), parsedMode))
            return new RaceActionViewModel { Message = "Choose push, standard, save tyres, or save fuel." };

        var engine = GetActiveRaceEngine();
        var car = engine.GetSnapshot().Cars.FirstOrDefault(item =>
            item.CarNumber == carNumber && item.TeamId == PlayerTeam.Id);
        if (car is null || car.Retired)
            return new RaceActionViewModel { Message = $"Car #{carNumber} is not available for a mode instruction." };

        engine.Submit(new SetDriverModeCommand(car.CarId, parsedMode));
        return new RaceActionViewModel
        {
            Succeeded = true,
            Message = $"Car #{carNumber} instructed to {parsedMode}."
        };
    }

    public RaceAdviceViewModel GetRaceAdvice(int carNumber)
    {
        var engine = GetActiveRaceEngine();
        var advice = GetTyreAdvice(engine.GetSnapshot(), carNumber);
        return new RaceAdviceViewModel
        {
            Message = advice.Message,
            SuggestedTyre = advice.SuggestedTyre.ToString(),
            RecommendPit = advice.RecommendPit
        };
    }

    public RaceSettlementViewModel SettleLiveRace()
    {
        _lastRaceSettlement = null;
        DispatchOrThrow(new SettleLiveRaceAction(PlayerTeam.Id));
        RunRivalPrincipalTurns();
        return _lastRaceSettlement ??
            throw new InvalidOperationException("Race settlement completed without a result.");
    }

    private RaceSettlementViewModel SettleLiveRaceCore()
    {
        var engine = GetActiveRaceEngine();
        var setup = _activeRaceSetup ??
            throw new InvalidOperationException("There is no active race setup.");
        if (!engine.IsFinished)
            throw new InvalidOperationException("The race must finish before settlement.");
        if (_liveRaceSettled)
            throw new InvalidOperationException("This race has already been settled.");

        var classification = engine.GetSnapshot().Cars.OrderBy(car => car.Position)
            .Select(car => $"P{car.Position} {car.DriverName} ({car.TeamName})").ToArray();
        var payouts = SettleRace(setup, engine.GetResult());
        _liveRaceSettled = true;
        CompleteNextRace();
        return new RaceSettlementViewModel
        {
            Classification = classification,
            Payouts = payouts.Select(item => new RacePayoutViewModel
            {
                TeamName = item.TeamName,
                AmountCents = item.Amount.Cents,
                IsPlayer = item.IsPlayer
            }).ToArray(),
            TeamBudgetCents = PlayerTeam.Budget.Cents
        };
    }

    public IReadOnlyList<CalendarEventViewModel> GetCalendar() =>
        _calendar.Select(item => new CalendarEventViewModel
        {
            Date = item.Date,
            Circuit = item.Circuit.Name,
            Country = item.Circuit.Country,
            RaceLaps = item.Circuit.RaceLaps,
            Characteristic = item.Circuit.Characteristic,
            Completed = item.Completed,
            Next = item == NextEvent
        }).ToArray();

    public IReadOnlyList<ChampionshipEntryViewModel> GetStandings() =>
        Teams.OrderByDescending(team => team.ChampionshipPoints)
            .Select((team, index) => new ChampionshipEntryViewModel
            {
                Position = index + 1,
                TeamName = team.Name,
                Points = team.ChampionshipPoints,
                IsPlayer = team == PlayerTeam
            }).ToArray();

    public StaffViewModel GetStaff() => new()
    {
        TeamName = PlayerTeam.Name,
        Engineers = PlayerTeam.Engineers.Select(engineer => new EngineerViewModel
        {
            Name = engineer.Name,
            Skill = engineer.Skill,
            AnnualSalaryCents = engineer.AnnualSalary.Cents,
            ContractYears = engineer.ContractYears
        }).ToArray(),
        Candidates = _engineerCandidates.Select((candidate, index) => new EngineerCandidateViewModel
        {
            MarketNumber = index + 1,
            Name = candidate.Name,
            Skill = candidate.Skill,
            AskingAnnualSalaryCents = candidate.AskingAnnualSalary.Cents,
            Hired = PlayerTeam.Engineers.Any(engineer => engineer.Id == candidate.Id)
        }).ToArray()
    };

    public FacilitiesViewModel GetFacilities() => new()
    {
        WindTunnelLevel = Facilities.WindTunnelLevel,
        SimulatorLevel = Facilities.SimulatorLevel,
        FactoryLevel = Facilities.FactoryLevel,
        WindTunnelUpgradeCostCents = GetUpgradeCost(Facilities.WindTunnelLevel, "wind").Cents,
        SimulatorUpgradeCostCents = GetUpgradeCost(Facilities.SimulatorLevel, "simulator").Cents,
        FactoryUpgradeCostCents = GetUpgradeCost(Facilities.FactoryLevel, "factory").Cents,
        BudgetRemainingCents = PlayerTeam.Budget.Cents
    };

    public DevelopmentViewModel GetDevelopment()
    {
        var project = _developmentProject;
        return new DevelopmentViewModel
        {
            Downforce = PlayerCarDesign.Downforce,
            DragEfficiency = PlayerCarDesign.DragEfficiency,
            Reliability = PlayerCarDesign.Reliability,
            ComponentLifeLaps = PlayerCarDesign.ComponentLifeLaps,
            CostCapSpentCents = PlayerCarDesign.CostCapSpentCents,
            CostCapLimitCents = _seriesRules.CostCapLimitCents,
            CostCapHeadroomCents = Math.Max(0, _seriesRules.CostCapLimitCents - PlayerCarDesign.CostCapSpentCents),
            HasActiveProject = project is not null &&
                project.Stage is not (DevelopmentStage.Complete or DevelopmentStage.Rejected),
            ProjectFocus = project?.Focus.ToString() ?? string.Empty,
            ProjectStage = project?.Stage.ToString() ?? string.Empty,
            ProjectProgress = project?.Progress ?? 0
        };
    }

    /// <summary>Builds a race grid for the next scheduled event.</summary>
    /// <param name="laps">Number of laps in this race.</param>
    /// <param name="seed">Seed used to make race simulation reproducible.</param>
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
                    CarPaceFactor = _teamPaceFactors[team.Id] +
                        (team == PlayerTeam ? PlayerCarDesign.PaceFactor - 1 -
                            (Weekend.PracticeComplete ? Weekend.RacePaceBonus : 0) : 0) +
                        driverIndex * 0.003,
                    StartingTyre = RaceTyreCompound.Medium,
                    FuelLoadKg = 110,
                    IsAi = team != PlayerTeam
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

    /// <summary>Runs practice using the setup focus.</summary>
    public IReadOnlyList<PracticeResult> RunPractice(int seed) =>
        RunPractice(seed, PracticeFocus.Setup);

    /// <summary>Runs practice and applies the selected focus to the weekend setup.</summary>
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
                (_teamPaceFactors[team.Id] + (team == PlayerTeam ? PlayerCarDesign.PaceFactor - 1 : 0)) *
                (1 - driver.Pace / 10000.0);
            var setupQuality = Facilities.PracticeBonus / 100.0;
            var tyreWearPerLap = RaceTyreModel.WearPerLap(
                RaceTyreCompound.Medium, circuit.TyreWearFactor, driver.TyreManagement) *
                (focus == PracticeFocus.TyreEvaluation ? 0.92 : 1);
            return new PracticeResult
            {
                DriverName = driver.Name,
                BestLapSeconds = baseLap - setupQuality +
                    (focus == PracticeFocus.Setup ? -0.12 : focus == PracticeFocus.TyreEvaluation ? 0.08 : 0) +
                    random.NextGaussian(0, 0.22),
                LongRunLapSeconds = baseLap * (1 + circuit.TyreWearFactor * 0.035) + random.NextGaussian(0, 0.3),
                TyreWearPerLap = tyreWearPerLap,
                EngineerNote = focus switch
                {
                    PracticeFocus.Setup => $"Setup confidence {Weekend.SetupConfidence:F0}%; {driver.Name} reports a predictable balance.",
                    PracticeFocus.TyreEvaluation => $"Tyre data recommends a conservative stint; estimated wear " +
                        $"{tyreWearPerLap:P1}/lap.",
                    _ => driver.Feedback >= 85
                        ? "Long-run simulation complete; race pace feedback is strong."
                        : "Long-run simulation complete; consider preserving tyres."
                }
            };
        }).OrderBy(result => result.BestLapSeconds).ToArray();
        Weekend.PracticeResults = results;
        Weekend.PracticeComplete = true;
        AddInboxMessage($"{Today:yyyy-MM-dd}: Practice completed at {circuit.Name}; staff reviewed the long-run data.");
        return results;
    }

    /// <summary>Runs qualifying with the standard soft-tyre strategy.</summary>
    public IReadOnlyList<QualifyingResult> RunQualifying(int seed) =>
        RunQualifying(seed, TyreCompoundType.Soft, QualiRisk.Normal, 2);

    /// <summary>Runs qualifying using the player's tyre, risk, and run-count choices.</summary>
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
                (_teamPaceFactors[team.Id] + (team == PlayerTeam ? PlayerCarDesign.PaceFactor - 1 : 0)) *
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
        AddInboxMessage($"{Today:yyyy-MM-dd}: Qualifying complete at {circuit.Name}; grid positions are confirmed.");
        return Weekend.QualifyingResults;
    }

    /// <summary>Returns live tyre strategy advice for one of the player's cars.</summary>
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
        var wearRate = RaceTyreModel.WearPerLap(
            car.Tyre, NextEvent.Circuit.TyreWearFactor, driver.TyreManagement);
        if (measuredWearRate.HasValue)
        {
            var standardWearRate = RaceTyreModel.WearPerLap(
                RaceTyreCompound.Medium, NextEvent.Circuit.TyreWearFactor, driver.TyreManagement);
            if (standardWearRate > 0)
                wearRate *= measuredWearRate.Value / standardWearRate;
        }
        var lapsRemaining = Math.Max(0, NextEvent.Circuit.RaceLaps - car.LapsCompleted);
        var engineer = PlayerTeam.Engineers.OrderByDescending(item => item.Skill).FirstOrDefault();
        var staffName = engineer?.Name ?? "Race engineer";
        var confidence = engineer?.Skill ?? 62;
        var pitWearThreshold = Math.Clamp(
            0.30 + (confidence - 70) * 0.001 + (driver.Feedback - 70) * 0.0005,
            0.24,
            0.42);
        var lapsUntilPitWindow = wearRate <= 0
            ? int.MaxValue
            : (int)Math.Ceiling(Math.Max(0, pitWearThreshold - car.TyreWear) / wearRate);
        var recommendPit = car.TyreWear >= pitWearThreshold ||
            (lapsRemaining > 2 && lapsUntilPitWindow <= 2);
        var suggestedTyre = lapsRemaining > 6 ? RaceTyreCompound.Hard : RaceTyreCompound.Soft;
        if (suggestedTyre == car.Tyre)
            suggestedTyre = RaceTyreCompound.Medium;
        var driverAdvice = driver.Feedback >= 85
            ? $"{driver.Name}: tyre feedback is clear; current wear is {car.TyreWear:P0}."
            : $"{driver.Name}: tyre feedback is uncertain; monitor wear at {car.TyreWear:P0}.";
        var staffAdvice = recommendPit
            ? $"{staffName} ({confidence}): box for {suggestedTyre}; the tyre window is here."
            : lapsUntilPitWindow < lapsRemaining
                ? $"{staffName} ({confidence}): stay out; estimated pit window in {lapsUntilPitWindow} lap(s)."
                : $"{staffName} ({confidence}): stay out; current tyres should last to the flag.";
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
                if (PlayerCarDesign.CostCapSpentCents + capAddition > _seriesRules.CostCapLimitCents)
                {
                    project.Stage = DevelopmentStage.Rejected;
                    AddInboxMessage("Regulatory office: The upgrade exceeds the cost cap and cannot be fitted.");
                    return new DevelopmentStageResult { Stage = project.Stage, Failed = true, Message = "Rejected: cost cap exceeded." };
                }
                PlayerCarDesign.Downforce = Math.Min(100, PlayerCarDesign.Downforce + project.DownforceDelta);
                PlayerCarDesign.DragEfficiency = Math.Min(100, PlayerCarDesign.DragEfficiency + project.DragDelta);
                PlayerCarDesign.Reliability = Math.Clamp(PlayerCarDesign.Reliability + project.ReliabilityDelta, 0, 100);
                PlayerCarDesign.ComponentLifeLaps = Math.Max(100, PlayerCarDesign.ComponentLifeLaps + project.LifespanDeltaLaps);
                PlayerCarDesign.CostCapSpentCents += capAddition;
                AddInboxMessage($"Chief engineer: {project.Focus} package completed; lifespan and regulatory checks passed.");
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
        AddInboxMessage($"Facilities manager: {facility} upgraded for {FormatMoney(cost)}.");
    }

    public void CompleteNextRace()
    {
        var next = NextEvent;
        next.Completed = true;
        Weekend.RaceComplete = true;
        if (_calendar.All(item => item.Completed))
            AddInboxMessage("Season review: all scheduled races are complete.");
        else
            Weekend = new RaceWeekend(NextEvent.Circuit.Name);
    }

    /// <summary>Applies race points and prize money to teams and drivers.</summary>
    public IReadOnlyList<TeamRacePayout> SettleRace(RaceSetup setup, RaceResult result)
    {
        var byId = setup.Grid.ToDictionary(car => car.CarId);
        var payouts = new Dictionary<EntityId, long>();
        var driverIds = new Dictionary<EntityId, EntityId>();
        for (var index = 0; index < result.Classification.Count; index++)
        {
            var car = byId[result.Classification[index]];
            var points = _seriesRules.PointsForPosition(index + 1);
            var prize = _seriesRules.PrizeCentsForPosition(index + 1);
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
            AddInboxMessage($"Race prize paid: {FormatMoney(playerPayout.Amount)} for {PlayerTeam.Name}.");
        return settlement;
    }

    public static string FormatMoney(Money money) =>
        (money.Cents / 100m).ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

    private CommandResult HandleStartDevelopment(StartDevelopmentAction command)
    {
        var focus = command.Focus.Trim().ToLowerInvariant() switch
        {
            "aero" or "aerodynamics" => (DevelopmentFocus?)DevelopmentFocus.Aerodynamics,
            "chassis" => DevelopmentFocus.Chassis,
            "reliability" => DevelopmentFocus.Reliability,
            _ => null
        };
        if (!focus.HasValue)
            return CommandResult.Fail("Choose aero, chassis, or reliability.");

        return HandleAction("development started", () =>
        {
            var result = StartDevelopment(focus.Value, new Money(command.BudgetCents));
            if (result.Failed)
                throw new InvalidOperationException(result.Message);
            return result.Message;
        });
    }

    private CommandResult HandleAdvanceCalendar(AdvanceCalendarAction command)
    {
        try
        {
            _lastAdvanceSummary = command.ToNextEvent
                ? AdvanceToNextEventCore()
                : AdvanceDaysCore(command.Days ??
                    throw new ArgumentException("A day count is required for calendar advancement."));
            return CompleteAction("calendar advanced", _lastAdvanceSummary.Message);
        }
        catch (InvalidOperationException exception)
        {
            return CommandResult.Fail(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return CommandResult.Fail(exception.Message);
        }
    }

    private CommandResult HandleRunPractice(RunPracticeAction command)
    {
        var normalizedFocus = command.Focus.Trim().Replace(" ", string.Empty)
            .Replace("-", string.Empty).ToLowerInvariant();
        var focus = normalizedFocus switch
        {
            "setup" => (PracticeFocus?)PracticeFocus.Setup,
            "tyreevaluation" or "tyres" => PracticeFocus.TyreEvaluation,
            "racesimulation" or "race" => PracticeFocus.RaceSimulation,
            _ => null
        };
        if (!focus.HasValue)
            return CommandResult.Fail("Choose setup, tyre evaluation, or race simulation.");

        return HandleAction("practice completed", () =>
        {
            RunPractice(command.Seed, focus.Value);
            return $"Practice completed with {focus.Value} focus.";
        });
    }

    private CommandResult HandleRunQualifying(RunQualifyingAction command)
    {
        if (!Enum.TryParse(command.Tyre, true, out TyreCompoundType tyre) ||
            !Enum.IsDefined(typeof(TyreCompoundType), tyre))
            return CommandResult.Fail("Choose a valid qualifying tyre compound.");
        if (!Enum.TryParse(command.Risk, true, out QualiRisk risk) ||
            !Enum.IsDefined(typeof(QualiRisk), risk))
            return CommandResult.Fail("Choose safe, normal, or push qualifying risk.");

        return HandleAction("qualifying completed", () =>
        {
            RunQualifying(command.Seed, tyre, risk, command.Runs);
            return "Qualifying completed and the grid is confirmed.";
        });
    }

    private CommandResult HandleQueuePitStop(QueuePitStopAction command)
    {
        var result = QueuePitStopCore(command.CarNumber, command.Tyre, command.ExtraDelaySeconds);
        return result.Succeeded
            ? CompleteAction("pit instruction queued", result.Message)
            : CommandResult.Fail(result.Message);
    }

    private CommandResult HandleSetRaceMode(SetRaceModeAction command)
    {
        var result = SetRaceModeCore(command.CarNumber, command.Mode);
        return result.Succeeded
            ? CompleteAction("driver mode changed", result.Message)
            : CommandResult.Fail(result.Message);
    }

    private CommandResult HandleAction(string action, Func<string> operation)
    {
        try
        {
            return CompleteAction(action, operation());
        }
        catch (InvalidOperationException exception)
        {
            return CommandResult.Fail(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return CommandResult.Fail(exception.Message);
        }
        catch (OverflowException exception)
        {
            return CommandResult.Fail(exception.Message);
        }
    }

    private CommandResult CompleteAction(string action, string message) =>
        CommandResult.Ok(new IDomainEvent[] { new SessionActionEvent(Today, action, message) });

    private CommandResult DispatchOrThrow(ICommand command)
    {
        var result = Execute(command);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Error ?? "The command was rejected.");
        return result;
    }

    private static SessionCommandRecord ToCommandRecord(ICommand command)
    {
        var record = command switch
        {
            HireEngineerCommand hire => new SessionCommandRecord
            {
                Type = "hire-engineer",
                Text1 = hire.CandidateId.ToString(),
                Long1 = hire.OfferedSalary.Cents,
                Int1 = hire.ContractYears
            },
            UpgradeFacilityAction upgrade => new SessionCommandRecord
            {
                Type = "upgrade-facility",
                Text1 = upgrade.Facility
            },
            StartDevelopmentAction development => new SessionCommandRecord
            {
                Type = "start-development",
                Text1 = development.Focus,
                Long1 = development.BudgetCents
            },
            AdvanceDevelopmentAction => new SessionCommandRecord { Type = "advance-development" },
            AdvanceCalendarAction advance => new SessionCommandRecord
            {
                Type = "advance-calendar",
                Int1 = advance.Days ?? 0,
                Bool1 = advance.ToNextEvent
            },
            RunPracticeAction practice => new SessionCommandRecord
            {
                Type = "run-practice",
                Int1 = practice.Seed,
                Text1 = practice.Focus
            },
            RunQualifyingAction qualifying => new SessionCommandRecord
            {
                Type = "run-qualifying",
                Int1 = qualifying.Seed,
                Int2 = qualifying.Runs,
                Text1 = qualifying.Tyre,
                Text2 = qualifying.Risk
            },
            StartRaceAction race => new SessionCommandRecord
            {
                Type = "start-race",
                Int1 = race.Laps,
                Int2 = race.Seed
            },
            QueuePitStopAction pit => new SessionCommandRecord
            {
                Type = "queue-pit-stop",
                Int1 = pit.CarNumber,
                Text1 = pit.Tyre,
                Double1 = pit.ExtraDelaySeconds
            },
            SetRaceModeAction mode => new SessionCommandRecord
            {
                Type = "set-race-mode",
                Int1 = mode.CarNumber,
                Text1 = mode.Mode
            },
            SettleLiveRaceAction => new SessionCommandRecord { Type = "settle-race" },
            AdvanceLiveRaceAction advanceRace => new SessionCommandRecord
            {
                Type = "advance-race",
                Double1 = advanceRace.ElapsedSeconds
            },
            MarkInboxReadAction => new SessionCommandRecord { Type = "mark-inbox-read" },
            _ => throw new InvalidOperationException(
                $"Command type {command.GetType().Name} cannot be stored in a session save.")
        };
        record.Issuer = command.Issuer.ToString();
        return record;
    }

    private static ICommand FromCommandRecord(SessionCommandRecord record)
    {
        if (record is null)
            throw new InvalidDataException("Save command history contains a null command.");

        var issuerText = RequireText(record.Issuer, nameof(record.Issuer));
        if (!Guid.TryParse(issuerText, out var issuerId))
            throw new InvalidDataException("Save command issuer is not a valid entity ID.");
        var issuer = new EntityId(issuerId);
        return record.Type switch
        {
            "hire-engineer" => new HireEngineerCommand(
                issuer,
                ParseEntityId(record.Text1, nameof(record.Text1)),
                new Money(record.Long1),
                record.Int1),
            "upgrade-facility" => new UpgradeFacilityAction(
                issuer,
                RequireText(record.Text1, nameof(record.Text1))),
            "start-development" => new StartDevelopmentAction(
                issuer,
                RequireText(record.Text1, nameof(record.Text1)),
                record.Long1),
            "advance-development" => new AdvanceDevelopmentAction(issuer),
            "advance-calendar" => new AdvanceCalendarAction(
                issuer,
                record.Bool1 ? null : record.Int1,
                record.Bool1),
            "run-practice" => new RunPracticeAction(
                issuer,
                record.Int1,
                RequireText(record.Text1, nameof(record.Text1))),
            "run-qualifying" => new RunQualifyingAction(
                issuer,
                record.Int1,
                RequireText(record.Text1, nameof(record.Text1)),
                RequireText(record.Text2, nameof(record.Text2)),
                record.Int2),
            "start-race" => new StartRaceAction(
                issuer,
                record.Int1,
                record.Int2),
            "queue-pit-stop" => new QueuePitStopAction(
                issuer,
                record.Int1,
                RequireText(record.Text1, nameof(record.Text1)),
                record.Double1),
            "set-race-mode" => new SetRaceModeAction(
                issuer,
                record.Int1,
                RequireText(record.Text1, nameof(record.Text1))),
            "settle-race" => new SettleLiveRaceAction(issuer),
            "advance-race" => new AdvanceLiveRaceAction(
                issuer,
                record.Double1),
            "mark-inbox-read" => new MarkInboxReadAction(issuer),
            _ => throw new InvalidDataException($"Unknown saved command type '{record.Type}'.")
        };
    }

    private static string RequireText(string? value, string property) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException($"Save command is missing {property}.");

    private static EntityId ParseEntityId(string? value, string property)
    {
        var text = RequireText(value, property);
        if (!Guid.TryParse(text, out var id))
            throw new InvalidDataException($"Save command {property} is not a valid entity ID.");
        return new EntityId(id);
    }

    private static SessionCommandRecord CloneCommandRecord(SessionCommandRecord command) => new()
    {
        Type = command.Type,
        Issuer = command.Issuer,
        Text1 = command.Text1,
        Text2 = command.Text2,
        Int1 = command.Int1,
        Int2 = command.Int2,
        Long1 = command.Long1,
        Double1 = command.Double1,
        Bool1 = command.Bool1
    };

    private static ActionResultViewModel ToActionResult(CommandResult result) =>
        new()
        {
            Succeeded = result.Succeeded,
            Message = result.Succeeded
                ? (result.Events.OfType<SessionActionEvent>().FirstOrDefault()?.Message ?? "Action completed.")
                : result.Error ?? "The command was rejected."
        };

    private static RaceActionViewModel ToRaceActionResult(CommandResult result) =>
        new()
        {
            Succeeded = result.Succeeded,
            Message = result.Succeeded
                ? (result.Events.OfType<SessionActionEvent>().FirstOrDefault()?.Message ?? "Race instruction accepted.")
                : result.Error ?? "The race instruction was rejected."
        };

    private void OnDayAdvanced(DateTime date)
    {
        if (date.Day != 1)
            return;

        foreach (var team in Teams)
        {
            var payroll = team.MonthlyPayroll;
            if (payroll.Cents > team.Budget.Cents)
            {
                AddInboxMessage($"{date:yyyy-MM-dd}: {team.Name} cannot cover payroll; team is insolvent.", date);
                continue;
            }

            team.Deduct(payroll);
            _eventBus.Publish(new PayrollPaidEvent(date, team.Id, payroll));
        }
        AddInboxMessage($"{date:yyyy-MM-dd}: monthly payroll processed.", date);
    }

    private string GetEngineerName(EntityId id) =>
        _engineerCandidates.First(candidate => candidate.Id == id).Name;

    private string GetTeamName(EntityId id) =>
        _teamsById.TryGetValue(id, out var team)
            ? team.Name
            : throw new InvalidOperationException($"Unknown team ID '{id}'.");

    private void AddInboxMessage(string message, DateTime? date = null)
    {
        _inbox.Add(message);
        _inboxDates.Add(date ?? Today);
    }

    private Money GetUpgradeCost(int level, string facility) =>
        level >= 5 ? new Money(0) : Facilities.UpgradeCost(facility);

    private IRaceEngine GetActiveRaceEngine()
    {
        if (_activeRaceEngine is null || _liveRaceSettled)
            throw new InvalidOperationException("There is no active race.");
        return _activeRaceEngine;
    }

    private bool HasPayrollIssueSince(int inboxIndex) =>
        _inbox.Skip(inboxIndex).Any(message => message.Contains(
            "cannot cover payroll", StringComparison.OrdinalIgnoreCase));

    private string GetAdvanceMessage(DateTime startDate, int daysAdvanced, bool stoppedForAttention)
    {
        if (stoppedForAttention)
            return $"Stopped after {daysAdvanced} day(s): a team needs attention for payroll.";
        if (daysAdvanced == 3650 && Today < NextEvent.Date)
            return "Stopped at the 10-year safety limit before the next event.";
        return $"Advanced {daysAdvanced} day(s) from {startDate:dd MMM yyyy} to {Today:dd MMM yyyy}.";
    }

    private Team GetTeamForDriver(Driver driver) =>
        Teams[_drivers.IndexOf(driver) / 2];

    private void RunRivalPrincipalTurns()
    {
        foreach (var team in Teams.Where(team => team != PlayerTeam))
        {
            var command = _teamPrincipalPolicy.ChooseAction(team, _engineerCandidates);
            if (command is null)
                continue;

            var result = Execute(command);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Rival team '{team.Name}' action was rejected: {result.Error}");
        }
    }

    private void SubmitAiRaceCommands()
    {
        var engine = GetActiveRaceEngine();
        var setup = _activeRaceSetup ??
            throw new InvalidOperationException("There is no active race setup.");
        foreach (var command in _aiRaceStrategyPolicy.ChooseCommands(setup, engine.GetSnapshot()))
            engine.Submit(command);
    }

    private EntityId GetDriverId(Driver driver) => _driverIds[driver];

    private readonly Dictionary<Driver, EntityId> _driverIds = new();

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
