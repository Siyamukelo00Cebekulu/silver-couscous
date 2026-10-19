using System;
using System.Collections.Generic;
using System.Linq;
using GrandPrix.Domain.Calendar;
using GrandPrix.Domain.Drivers;

namespace GrandPrix.Domain.Race
{
    public enum WeekendPhase { Practice, Qualifying, Race, Complete }

    public class WeekendEntry
    {
        public Driver Driver { get; set; } = new Driver();
        public bool IsPlayer { get; set; }
    }

    public class WeekendState
    {
        public Track Track { get; }
        public Weather Weather { get; }
        public List<WeekendEntry> Entries { get; }
        public WeekendPhase Phase { get; private set; } = WeekendPhase.Practice;

        public PracticeResult? PlayerPracticeResult { get; private set; }
        public List<QualifyingResult> QualifyingResults { get; private set; } = new();
        public List<DriverRaceResult> RaceResults { get; private set; } = new();

        private readonly int _seed;

        public WeekendState(Track track, Weather weather, IEnumerable<WeekendEntry> entries, int seed)
        {
            Track = track;
            Weather = weather;
            Entries = entries.ToList();
            _seed = seed;
        }

        public PracticeResult RunPractice(PracticePlan plan)
        {
            RequirePhase(WeekendPhase.Practice, "Practice");

            var player = Entries.First(e => e.IsPlayer).Driver;
            var sim = new PracticeSimulator(Track, Weather, _seed);
            PlayerPracticeResult = sim.Run(player, plan);

            Phase = WeekendPhase.Qualifying;
            return PlayerPracticeResult;
        }

        public List<QualifyingResult> RunQualifying(QualifyingPlan playerPlan)
        {
            RequirePhase(WeekendPhase.Qualifying, "Qualifying");

            var sim = new QualifyingSimulator(Track, Weather, _seed + 1);
            double playerSetup = PlayerPracticeResult?.SetupConfidence ?? 60;

            var inputs = Entries.Select(entry => (
                driver: entry.Driver,
                plan: entry.IsPlayer
                    ? playerPlan
                    : new QualifyingPlan
                    {
                        Tyre = TyreCompoundType.Soft,
                        Risk = QualiRisk.Normal,
                        Runs = 2
                    },
                setupConfidence: entry.IsPlayer ? playerSetup : 60
            ));

            QualifyingResults = sim.SimulateSession(inputs);
            Phase = WeekendPhase.Race;
            return QualifyingResults;
        }

        public List<DriverRaceResult> RunRace(StrategyPlan playerPlan)
        {
            RequirePhase(WeekendPhase.Race, "Race");

            var sim = new RaceSimulator(Track, _seed + 2);
            var results = new List<DriverRaceResult>();

            foreach (var entry in Entries)
            {
                StrategyPlan plan = entry.IsPlayer
                    ? playerPlan
                    : new StrategyPlan
                    {
                        StartingTyre = TyreCompoundType.Medium,
                        PitLap = Track.Laps / 2,
                        PitTyre = TyreCompoundType.Medium,
                        Pace = PaceMode.Normal
                    };

                var result = sim.Simulate(entry.Driver, plan);
                result.DriverName = entry.Driver.Name;
                results.Add(result);
            }

            RaceResults = results.OrderBy(r => r.TotalTime).ToList();
            Phase = WeekendPhase.Complete;
            return RaceResults;
        }

        private void RequirePhase(WeekendPhase expected, string name)
        {
            if (Phase != expected)
                throw new InvalidOperationException(
                    $"{name} cannot run. Current phase: {Phase}.");
        }
    }
}