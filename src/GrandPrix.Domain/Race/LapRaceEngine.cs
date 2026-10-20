using System;
using System.Collections.Generic;
using System.Linq;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Common;

namespace GrandPrix.Domain.Race;

public sealed class LapRaceEngine : IRaceEngine
{
    private readonly Dictionary<EntityId, CarRuntime> _cars = new();
    private readonly List<RaceEvent> _eventLog = new();
    private readonly List<IRaceCommand> _queuedCommands = new();
    private RaceSetup? _setup;
    private TimeSpan _raceTime;
    private double _fastestLap = double.MaxValue;
    private bool _started;
    private bool _completionEventRaised;

    public bool IsFinished => _started && _cars.Values.All(car => car.Finished || car.Retired);

    public IReadOnlyList<RaceEvent> AdvanceOneLap()
    {
        EnsureStarted();
        if (IsFinished)
            return Array.Empty<RaceEvent>();

        var targetLap = _cars.Values
            .Where(car => !car.Finished && !car.Retired)
            .Min(car => car.LapsCompleted) + 1;
        var events = new List<RaceEvent>();
        while (!IsFinished && _cars.Values
                   .Where(car => !car.Finished && !car.Retired)
                   .Any(car => car.LapsCompleted < targetLap))
        {
            var seconds = _cars.Values
                .Where(car => !car.Finished && !car.Retired)
                .Min(car => car.LapRemainingSeconds);
            events.AddRange(Advance(TimeSpan.FromSeconds(Math.Max(seconds, 0.000001))));
        }

        return events;
    }

    public void Start(RaceSetup setup, IRandom random)
    {
        if (setup is null)
            throw new ArgumentNullException(nameof(setup));
        if (random is null)
            throw new ArgumentNullException(nameof(random));
        if (_started)
            throw new InvalidOperationException("This race engine has already started.");
        if (setup.TotalLaps <= 0)
            throw new ArgumentOutOfRangeException(nameof(setup), "The race must have at least one lap.");
        if (setup.Grid is null || setup.Grid.Count == 0)
            throw new ArgumentException("The race grid cannot be empty.", nameof(setup));
        if (setup.Grid.Any(car => car is null || car.CarId == default || car.DriverId == default))
            throw new ArgumentException("Every grid car must have a car ID and a driver ID.", nameof(setup));
        if (setup.Grid.Select(car => car.CarId).Distinct().Count() != setup.Grid.Count)
            throw new ArgumentException("Car IDs must be unique.", nameof(setup));
        if (setup.Grid.Select(car => car.CarNumber).Distinct().Count() != setup.Grid.Count)
            throw new ArgumentException("Car numbers must be unique.", nameof(setup));
        if (!IsFinite(setup.BaseLapTimeSeconds) || setup.BaseLapTimeSeconds <= 0 ||
            !IsFinite(setup.TyreWearFactor) || setup.TyreWearFactor < 0 ||
            !IsFinite(setup.PitLossTimeSeconds) || setup.PitLossTimeSeconds < 0 ||
            !IsFinite(setup.OvertakingDifficulty) || setup.OvertakingDifficulty < 0)
            throw new ArgumentException("Race timing and tyre wear values must be valid.", nameof(setup));
        if (setup.Forecast is null || setup.Forecast.Any(point =>
                point is null || point.At < TimeSpan.Zero ||
                !IsFinite(point.RainIntensity) || point.RainIntensity < 0 || point.RainIntensity > 1 ||
                !IsFinite(point.TrackTempC)))
            throw new ArgumentException("Weather forecast values must be valid.", nameof(setup));
        if (setup.Grid.Any(car => !Enum.IsDefined(typeof(RaceTyreCompound), car.StartingTyre) ||
                !IsFinite(car.FuelLoadKg) || car.FuelLoadKg < 0 ||
                !IsFinite(car.CarPaceFactor) || car.CarPaceFactor <= 0 ||
                car.DriverPace < 0 || car.DriverPace > 100 ||
                car.Consistency < 0 || car.Consistency > 100 ||
                car.TyreManagement < 0 || car.TyreManagement > 100))
            throw new ArgumentException("Grid tyre and fuel values must be valid.", nameof(setup));

        _setup = setup;
        _started = true;
        foreach (var car in setup.Grid)
        {
            var runtime = new CarRuntime(car, random.Fork($"car:{car.CarId}"));
            _cars.Add(car.CarId, runtime);
        }
        foreach (var car in _cars.Values)
            PrepareLap(car, null);
    }

    public void Submit(IRaceCommand command)
    {
        EnsureStarted();
        if (command is null)
            throw new ArgumentNullException(nameof(command));
        if (!_cars.TryGetValue(command.CarId, out var car))
            throw new ArgumentException("The command references a car that is not in this race.", nameof(command));
        if (car.Finished || car.Retired)
            throw new InvalidOperationException("Commands cannot be submitted for a finished car.");
        if (command is BoxThisLapCommand box &&
            (box.DelaySeconds < 0 || double.IsNaN(box.DelaySeconds) || double.IsInfinity(box.DelaySeconds)))
            throw new ArgumentOutOfRangeException(nameof(command), "Pit delay must be a finite, non-negative number.");
        if (command is BoxThisLapCommand tyreCommand && !Enum.IsDefined(typeof(RaceTyreCompound), tyreCommand.NextTyre))
            throw new ArgumentOutOfRangeException(nameof(command), "The requested tyre compound is not supported.");
        if (command is SetDriverModeCommand modeCommand && !Enum.IsDefined(typeof(DriverMode), modeCommand.Mode))
            throw new ArgumentOutOfRangeException(nameof(command), "The requested driver mode is not supported.");
        if (command is not BoxThisLapCommand && command is not SetDriverModeCommand)
            throw new ArgumentException($"Unsupported race command: {command.GetType().Name}.", nameof(command));

        _queuedCommands.Add(command);
    }

    public IReadOnlyList<RaceEvent> Advance(TimeSpan elapsed)
    {
        EnsureStarted();
        if (elapsed <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Elapsed time must be greater than zero.");

        var emitted = new List<RaceEvent>();
        ApplyQueuedCommands();
        var remainingSeconds = elapsed.TotalSeconds;

        while (remainingSeconds > 0 && !IsFinished)
        {
            var activeCars = _cars.Values.Where(car => !car.Finished && !car.Retired).ToArray();
            var oldOrder = GetOrderedCars().Select(car => car.Setup.CarId).ToList();
            var step = Math.Min(remainingSeconds, activeCars.Min(car => car.LapRemainingSeconds));
            foreach (var car in activeCars)
                car.LapRemainingSeconds -= step;

            remainingSeconds -= step;
            _raceTime += TimeSpan.FromSeconds(step);

            var completedCars = activeCars
                .Where(car => car.LapRemainingSeconds <= 0.000001)
                .ToArray();
            if (completedCars.Length == 0)
            {
                EmitOvertakes(oldOrder, emitted);
                continue;
            }

            foreach (var car in completedCars)
                CompleteLap(car, emitted);

            foreach (var car in completedCars.Where(car => !car.Finished && !car.Retired))
                PrepareLap(car, emitted);
            EmitOvertakes(oldOrder, emitted);
        }

        if (IsFinished && !_completionEventRaised)
        {
            _completionEventRaised = true;
            var completion = new RaceCompletedEvent
            {
                RaceTime = _raceTime,
                Lap = _setup!.TotalLaps
            };
            Emit(completion, emitted);
        }

        return emitted;
    }

    public RaceSnapshot GetSnapshot()
    {
        EnsureStarted();
        var ordered = GetOrderedCars();
        var leader = ordered[0];
        var states = new List<CarState>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var car = ordered[i];
            var leaderGap = GetGapSeconds(leader, car);
            var aheadGap = i == 0
                ? 0
                : GetGapSeconds(ordered[i - 1], car);

            states.Add(new CarState
            {
                CarId = car.Setup.CarId,
                TeamId = car.Setup.TeamId,
                TeamName = car.Setup.TeamName,
                DriverName = car.Setup.DriverName,
                CarNumber = car.Setup.CarNumber,
                Position = i + 1,
                LapsCompleted = car.LapsCompleted,
                TrackProgress = car.Finished ? 1 : GetLapProgress(car),
                GapToLeaderSec = leaderGap,
                GapToAheadSec = aheadGap,
                Tyre = car.CurrentTyre,
                TyreWear = car.TyreWear,
                FuelKg = Math.Max(0, car.Setup.FuelLoadKg - car.LapsCompleted * 1.8),
                Mode = car.CurrentMode,
                InPit = car.PitStops > 0 && car.PitOnCurrentLap,
                Retired = car.Retired,
                LastLapTimeSec = car.LastLapTimeSeconds,
                TotalTimeSec = car.TotalTimeSeconds
            });
        }

        return new RaceSnapshot
        {
            RaceTime = _raceTime,
            CurrentLap = Math.Min(
                _setup!.TotalLaps,
                ordered.Where(car => !car.Finished && !car.Retired)
                    .Select(car => car.LapsCompleted)
                    .DefaultIfEmpty(_setup.TotalLaps - 1)
                    .Min() + 1),
            Cars = states
        };
    }

    public RaceResult GetResult()
    {
        EnsureStarted();
        if (!IsFinished)
            throw new InvalidOperationException("The race result is not available until the race is finished.");

        return new RaceResult
        {
            Classification = GetOrderedCars().Select(car => car.Setup.CarId).ToArray(),
            FullEventLog = _eventLog.ToArray()
        };
    }

    private void ApplyQueuedCommands()
    {
        foreach (var command in _queuedCommands)
        {
            var car = _cars[command.CarId];
            switch (command)
            {
                case BoxThisLapCommand box:
                    car.NextTyre = box.NextTyre;
                    car.NextPitDelaySeconds = box.DelaySeconds;
                    break;
                case SetDriverModeCommand mode:
                    car.NextMode = mode.Mode;
                    break;
            }
        }
        _queuedCommands.Clear();
    }

    private void PrepareLap(CarRuntime car, List<RaceEvent>? emitted)
    {
        var lap = car.LapsCompleted + 1;
        car.PitOnCurrentLap = false;
        car.CurrentMode = car.NextMode ?? car.CurrentMode;
        car.NextMode = null;

        if (car.NextTyre.HasValue)
        {
            car.CurrentTyre = car.NextTyre.Value;
            car.TyreWear = 0;
            car.PitOnCurrentLap = true;
            var pitDelay = _setup!.PitLossTimeSeconds + car.NextPitDelaySeconds;
            var stationaryTime = Math.Max(1.5, 2.2 + car.Random.NextGaussian(0, 0.25));
            car.CurrentPitDelaySeconds = pitDelay + stationaryTime;
            car.PitStops++;
            Emit(new PitStopEvent
            {
                RaceTime = _raceTime,
                Lap = lap,
                CarId = car.Setup.CarId,
                DriverName = car.Setup.DriverName,
                Fitted = car.CurrentTyre,
                StationaryTimeSec = stationaryTime,
                PitLaneDelaySec = pitDelay
            }, emitted);
            car.NextTyre = null;
            car.NextPitDelaySeconds = 0;
        }
        else if (car.Setup.IsAi && lap == car.Setup.AiPitLap)
        {
            car.CurrentTyre = ChooseNextTyre(car.CurrentTyre);
            car.TyreWear = 0;
            car.PitOnCurrentLap = true;
            var stationaryTime = Math.Max(1.5, 2.2 + car.Random.NextGaussian(0, 0.25));
            car.CurrentPitDelaySeconds = _setup!.PitLossTimeSeconds + stationaryTime;
            car.PitStops++;
            Emit(new PitStopEvent
            {
                RaceTime = _raceTime,
                Lap = lap,
                CarId = car.Setup.CarId,
                DriverName = car.Setup.DriverName,
                Fitted = car.CurrentTyre,
                StationaryTimeSec = stationaryTime,
                PitLaneDelaySec = _setup!.PitLossTimeSeconds
            }, emitted);
        }

        if (car.Setup.IsAi)
        {
            car.CurrentMode = car.TyreWear >= 0.55
                ? DriverMode.SaveTyres
                : lap > _setup!.TotalLaps * 0.75
                    ? DriverMode.Push
                    : DriverMode.Standard;
        }

        car.LapDurationSeconds = CalculateLapTime(car);
        if (car.PitOnCurrentLap)
            car.LapDurationSeconds += car.CurrentPitDelaySeconds;
        car.LapRemainingSeconds = car.LapDurationSeconds;
    }

    private void CompleteLap(CarRuntime car, List<RaceEvent> emitted)
    {
        car.LapsCompleted++;
        car.TotalTimeSeconds += car.LapDurationSeconds;
        car.LastLapTimeSeconds = car.LapDurationSeconds - (car.PitOnCurrentLap
            ? car.CurrentPitDelaySeconds
            : 0);
        car.TyreWear = Math.Min(1, car.TyreWear + GetTyreWearRate(car.CurrentTyre) *
            _setup!.TyreWearFactor * (1 - car.Setup.TyreManagement / 100.0 * 0.3) / 100.0);

        Emit(new LapCompletedEvent
        {
            RaceTime = _raceTime,
            Lap = car.LapsCompleted,
            CarId = car.Setup.CarId,
            DriverName = car.Setup.DriverName,
            LapTimeSec = car.LastLapTimeSeconds
        }, emitted);

        if (car.LastLapTimeSeconds < _fastestLap)
        {
            _fastestLap = car.LastLapTimeSeconds;
            Emit(new FastestLapEvent
            {
                RaceTime = _raceTime,
                Lap = car.LapsCompleted,
                CarId = car.Setup.CarId,
                DriverName = car.Setup.DriverName,
                LapTimeSec = car.LastLapTimeSeconds
            }, emitted);
        }

        if (car.LapsCompleted >= _setup!.TotalLaps)
        {
            car.Finished = true;
            var ordered = GetOrderedCars();
            Emit(new CarFinishedEvent
            {
                RaceTime = _raceTime,
                Lap = car.LapsCompleted,
                CarId = car.Setup.CarId,
                DriverName = car.Setup.DriverName,
                Position = ordered.FindIndex(other => other == car) + 1
            }, emitted);
        }
    }

    private void EmitOvertakes(List<EntityId> oldOrder, List<RaceEvent> emitted)
    {
        var newOrder = GetOrderedCars();
        var newPositions = newOrder
            .Select((car, index) => (car.Setup.CarId, Position: index))
            .ToDictionary(item => item.CarId, item => item.Position);
        for (var oldBehindIndex = 1; oldBehindIndex < oldOrder.Count; oldBehindIndex++)
        {
            var attackerId = oldOrder[oldBehindIndex];
            var attacker = _cars[attackerId];
            for (var oldAheadIndex = oldBehindIndex - 1; oldAheadIndex >= 0; oldAheadIndex--)
            {
                var defenderId = oldOrder[oldAheadIndex];
                if (newPositions[attackerId] >= newPositions[defenderId])
                    continue;

                var defender = _cars[defenderId];
                Emit(new OvertakeEvent
                {
                    RaceTime = _raceTime,
                    Lap = Math.Max(attacker.LapsCompleted, defender.LapsCompleted),
                    Attacker = attackerId,
                    Defender = defenderId,
                    AttackerName = attacker.Setup.DriverName,
                    DefenderName = defender.Setup.DriverName,
                    NewPosition = newPositions[attackerId] + 1
                }, emitted);
            }
        }
    }

    private double CalculateLapTime(CarRuntime car)
    {
        var driverSkillFactor = 1 - car.Setup.DriverPace / 100.0 * 0.04;
        var gripFactor = 1 / GetGrip(car.CurrentTyre);
        var wearFactor = 1 + car.TyreWear * 0.12;
        var modeFactor = car.CurrentMode switch
        {
            DriverMode.Push => 0.985,
            DriverMode.SaveTyres => 1.015,
            DriverMode.SaveFuel => 1.01,
            _ => 1.0
        };
        var consistency = car.Setup.Consistency / 100.0;
        var variance = Math.Max(0.9, Math.Min(1.1,
            1 + car.Random.NextGaussian(0, (1 - consistency) * 0.012)));
        var weather = GetWeatherAt(_raceTime);
        var weatherFactor = weather.RainIntensity > 0.1 &&
            car.CurrentTyre is not (RaceTyreCompound.Intermediate or RaceTyreCompound.Wet)
                ? 1 + weather.RainIntensity * 0.18
                : 1;

        var order = _cars.Values
            .Where(other => other.LapDurationSeconds > 0 && !other.Finished && !other.Retired)
            .OrderByDescending(GetDistance)
            .ThenBy(other => other.TotalTimeSeconds + other.LapDurationSeconds - other.LapRemainingSeconds)
            .ToList();
        var index = order.IndexOf(car);
        var dirtyAirFactor = index > 0 &&
            GetDistance(order[index - 1]) - GetDistance(car) < 0.12
                ? 1 + Math.Max(0, _setup!.OvertakingDifficulty) * 0.006
                : 1;

        return _setup!.BaseLapTimeSeconds * car.Setup.CarPaceFactor * driverSkillFactor *
            gripFactor * wearFactor * modeFactor * variance * weatherFactor * dirtyAirFactor;
    }

    private WeatherPoint GetWeatherAt(TimeSpan time)
    {
        var forecast = _setup!.Forecast;
        var point = forecast.Where(item => item.At <= time).OrderBy(item => item.At).LastOrDefault();
        return point ?? new WeatherPoint();
    }

    private static double GetGrip(RaceTyreCompound tyre) => tyre switch
    {
        RaceTyreCompound.Soft => 1.03,
        RaceTyreCompound.Medium => 1,
        RaceTyreCompound.Hard => 0.97,
        RaceTyreCompound.Intermediate => 0.94,
        RaceTyreCompound.Wet => 0.91,
        _ => throw new ArgumentOutOfRangeException(nameof(tyre))
    };

    private static double GetTyreWearRate(RaceTyreCompound tyre) => tyre switch
    {
        RaceTyreCompound.Soft => 3.2,
        RaceTyreCompound.Medium => 2.2,
        RaceTyreCompound.Hard => 1.5,
        RaceTyreCompound.Intermediate => 2.7,
        RaceTyreCompound.Wet => 2.4,
        _ => throw new ArgumentOutOfRangeException(nameof(tyre))
    };

    private static RaceTyreCompound ChooseNextTyre(RaceTyreCompound current) =>
        current == RaceTyreCompound.Soft ? RaceTyreCompound.Hard : RaceTyreCompound.Soft;

    private double GetDistance(CarRuntime car) =>
        car.LapsCompleted + (car.Finished ? 0 : GetLapProgress(car));

    private double GetGapSeconds(CarRuntime ahead, CarRuntime behind)
    {
        if (ahead.Finished && behind.Finished)
            return Math.Max(0, behind.TotalTimeSeconds - ahead.TotalTimeSeconds);

        return Math.Max(0, GetDistance(ahead) - GetDistance(behind)) * _setup!.BaseLapTimeSeconds;
    }

    private static double GetLapProgress(CarRuntime car) =>
        car.LapDurationSeconds <= 0
            ? 0
            : Math.Max(0, Math.Min(1, 1 - car.LapRemainingSeconds / car.LapDurationSeconds));

    private List<CarRuntime> GetOrderedCars() =>
        _cars.Values
            .OrderByDescending(GetDistance)
            .ThenBy(car => car.Finished
                ? car.TotalTimeSeconds
                : car.TotalTimeSeconds + car.LapDurationSeconds - car.LapRemainingSeconds)
            .ThenBy(car => car.Setup.CarNumber)
            .ToList();

    private void Emit(RaceEvent raceEvent, List<RaceEvent>? emitted)
    {
        _eventLog.Add(raceEvent);
        emitted?.Add(raceEvent);
    }

    private void EnsureStarted()
    {
        if (!_started)
            throw new InvalidOperationException("Start the race before using the race engine.");
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private sealed class CarRuntime
    {
        public CarRuntime(CarSetup setup, IRandom random)
        {
            Setup = setup;
            Random = random;
            CurrentTyre = setup.StartingTyre;
        }

        public CarSetup Setup { get; }
        public IRandom Random { get; }
        public RaceTyreCompound CurrentTyre { get; set; }
        public DriverMode CurrentMode { get; set; } = DriverMode.Standard;
        public DriverMode? NextMode { get; set; }
        public RaceTyreCompound? NextTyre { get; set; }
        public double NextPitDelaySeconds { get; set; }
        public double CurrentPitDelaySeconds { get; set; }
        public double LapDurationSeconds { get; set; }
        public double LapRemainingSeconds { get; set; }
        public double TotalTimeSeconds { get; set; }
        public double LastLapTimeSeconds { get; set; }
        public double TyreWear { get; set; }
        public int LapsCompleted { get; set; }
        public int PitStops { get; set; }
        public bool PitOnCurrentLap { get; set; }
        public bool Finished { get; set; }
        public bool Retired { get; set; }
    }
}
