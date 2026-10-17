using System;
using System.Collections.Generic;
using System.Linq;
using GrandPrix.Core.Models;

namespace GrandPrix.Core.Simulation
{
    public enum QualiRisk { Safe, Normal, Push }

    public class QualifyingPlan
    {
        public TyreCompoundType Tyre { get; set; } = TyreCompoundType.Soft;
        public QualiRisk Risk { get; set; } = QualiRisk.Normal;
        public int Runs { get; set; } = 2;
    }

    public class QualifyingResult
    {
        public string DriverName { get; set; } = "";
        public double BestLapTime { get; set; }
        public int GridPosition { get; set; }
    }

    public class QualifyingSimulator
    {
        private readonly Random _rng;
        private readonly Track _track;
        private readonly Weather _weather;

        public QualifyingSimulator(Track track, Weather weather, int seed)
        {
            _track = track;
            _weather = weather;
            _rng = new Random(seed);
        }

        public double SimulateBestLap(Driver driver, QualifyingPlan plan, double setupConfidence)
        {
            var compound = GetCompound(plan.Tyre);

            double skill = 1 - (driver.Pace / 100.0) * 0.04;
            double grip = 1.0 / compound.Grip;
            double setupBoost = 1 - (setupConfidence / 100.0) * 0.01; // up to 1% gain

            double riskFactor = plan.Risk switch
            {
                QualiRisk.Push => 0.985,
                QualiRisk.Safe => 1.010,
                _ => 1.000
            };

            double riskVariance = plan.Risk switch
            {
                QualiRisk.Push => 0.020,
                QualiRisk.Safe => 0.005,
                _ => 0.010
            };

            // Wet session penalty if wrong tyre for conditions
            double weatherPenalty = WeatherPenalty(compound, _weather);

            double best = double.MaxValue;
            for (int i = 0; i < Math.Max(1, plan.Runs); i++)
            {
                double variance = 1 + (_rng.NextDouble() - 0.5) * riskVariance;
                double lap = _track.BaseLapTime * skill * grip * setupBoost
                             * riskFactor * variance * weatherPenalty;
                if (lap < best) best = lap;
            }
            return best;
        }

        public List<QualifyingResult> SimulateSession(
            IEnumerable<(Driver driver, QualifyingPlan plan, double setupConfidence)> entries)
        {
            var results = entries
                .Select(e => new QualifyingResult
                {
                    DriverName = e.driver.Name,
                    BestLapTime = SimulateBestLap(e.driver, e.plan, e.setupConfidence)
                })
                .OrderBy(r => r.BestLapTime)
                .ToList();

            for (int i = 0; i < results.Count; i++)
                results[i].GridPosition = i + 1;

            return results;
        }

        private static double WeatherPenalty(TyreCompound compound, Weather weather)
        {
            bool isWetTyre = compound.Type == TyreCompoundType.Intermediate
                          || compound.Type == TyreCompoundType.Wet;

            return weather.Condition switch
            {
                WeatherCondition.Dry when isWetTyre => 1.08,
                WeatherCondition.LightRain when !isWetTyre => 1.06,
                WeatherCondition.HeavyRain when !isWetTyre => 1.15,
                _ => 1.0
            };
        }

        private static TyreCompound GetCompound(TyreCompoundType type) => type switch
        {
            TyreCompoundType.Soft => TyreCompound.Soft,
            TyreCompoundType.Hard => TyreCompound.Hard,
            _ => TyreCompound.Medium
        };
    }
}