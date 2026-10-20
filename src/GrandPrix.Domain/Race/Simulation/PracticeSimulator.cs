using System;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Simulation;
using GrandPrix.Domain.Calendar;
using GrandPrix.Domain.Drivers;
using GrandPrix.Domain.Race;

namespace GrandPrix.Domain.Race
{
    public enum PracticeFocus { Setup, TyreEvaluation, RaceSimulation }

    public class PracticePlan
    {
        public PracticeFocus Focus { get; set; } = PracticeFocus.Setup;
    }

    public class PracticeResult
    {
        public string DriverName { get; set; } = "";
        public double BestLapSeconds { get; set; }
        public double LongRunLapSeconds { get; set; }
        public double TyreWearPerLap { get; set; }
        public string EngineerNote { get; set; } = "";
        public PracticeFocus Focus { get; set; }
        public double SetupConfidence { get; set; }   // 0-100, boosts quali & race
        public double TyreWearEstimate { get; set; }  // wear per lap
        public double LongRunPaceEstimate { get; set; }
        public string Report { get; set; } = "";
    }

    public class PracticeSimulator
    {
        private readonly IRandom _rng;
        private readonly Track _track;
        private readonly Weather _weather;

        public PracticeSimulator(Track track, Weather weather, int seed)
        {
            _track = track;
            _weather = weather;
            _rng = new SeededRandom(seed).Fork("practice");
        }

        public PracticeResult Run(Driver driver, PracticePlan plan)
        {
            var result = new PracticeResult { Focus = plan.Focus };

            double pace = driver.Pace / 100.0;
            double consistency = driver.Consistency / 100.0;

            switch (plan.Focus)
            {
                case PracticeFocus.Setup:
                    // Better consistency → better setup. Small random element.
                    result.SetupConfidence = 40 + consistency * 40 + _rng.NextDouble() * 20;
                    result.Report =
                        $"Setup confidence: {result.SetupConfidence:F0}%. " +
                        "Balance feels predictable; driver reports good rear stability.";
                    break;

                case PracticeFocus.TyreEvaluation:
                    result.TyreWearEstimate = 2.2 * _track.TyreWearFactor
                        * (1 - (driver.TyreManagement / 100.0) * 0.3);
                    result.Report =
                        $"Estimated tyre wear on Mediums: {result.TyreWearEstimate:F2}/lap. " +
                        $"Projected pit window around lap {EstimatePitWindow(result.TyreWearEstimate)}.";
                    break;

                case PracticeFocus.RaceSimulation:
                    result.LongRunPaceEstimate = _track.BaseLapTime
                        * (1 - pace * 0.04)
                        * 1.01
                        * (1 + _rng.NextDouble() * 0.005 - 0.0025);
                    result.Report =
                        $"Long-run pace estimate: {result.LongRunPaceEstimate:F3}s/lap. " +
                        "Fuel-corrected, on used Mediums.";
                    break;
            }

            return result;
        }

        private static int EstimatePitWindow(double wearPerLap)
        {
            // Assume ~75 units of usable tyre life before cliff
            return Math.Max(5, (int)(75 / Math.Max(0.1, wearPerLap)));
        }
    }
}