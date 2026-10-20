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
using GrandPrix.Domain.Drivers;
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
    }

    public Team PlayerTeam { get; }
    public IReadOnlyList<Team> Teams { get; }
    public IReadOnlyList<Driver> Drivers { get; }
    public IReadOnlyList<EngineerCandidate> EngineerCandidates => _engineerCandidates;
    public DateTime Today => _clock.Today;
    public IReadOnlyList<string> Inbox => _inbox;

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
            NextEvent = "Caspian Grand Prix",
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
                    CarPaceFactor = TeamPaceFactor(teamIndex) + driverIndex * 0.003,
                    StartingTyre = RaceTyreCompound.Medium,
                    FuelLoadKg = 110,
                    IsAi = team != PlayerTeam,
                    AiPitLap = team == PlayerTeam ? 0 : Math.Max(2, laps / 2)
                });
            }
        }

        return new RaceSetup
        {
            TrackId = "caspian-park",
            TotalLaps = laps,
            Seed = seed,
            BaseLapTimeSeconds = 96.4,
            TyreWearFactor = 1.05,
            PitLossTimeSeconds = 19.5,
            OvertakingDifficulty = 0.8,
            Grid = grid
        };
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
                WetSkill = consistency
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
