using System.Collections.Generic;
using GrandPrix.Core.Models;


namespace GrandPrix.Core.Simulation;

public class RaceResult
{
    public string DriverName { get; set; } = "";
    public double TotalTime { get; set; }
    public int PitStops { get; set; }
}

public class RaceSimulator
{
    private readonly LapCalculator _lapCalc;
    private readonly Track _track;

    public RaceSimulator(Track track, int seed)
    {
        _track = track;
        _lapCalc = new LapCalculator(seed);
    }

    public RaceResult Simulate(Driver driver, StrategyPlan plan)
    {
        var startingTyre = GetCompound(plan.StartingTyre);
        var pitTyre = GetCompound(plan.PitTyre);

        double tyreWear = 0;
        double totalTime = 0;
        int pits = 0;
        var currentTyre = startingTyre;

        for (int lap = 1; lap <= _track.Laps; lap++)
        {
            // Pit stop?
            if (lap == plan.PitLap)
            {
                totalTime += _track.PitLossTime;
                currentTyre = pitTyre;
                tyreWear = 0;
                pits++;
            }

            // Lap time
            totalTime += _lapCalc.CalculateLapTime(
                driver, currentTyre, tyreWear, _track, plan.Pace);

            // Tyre wear accumulates, reduced by driver skill
            double wearMultiplier = 1.0 - (driver.TyreManagement / 100.0) * 0.3;
            tyreWear += currentTyre.WearRate * _track.TyreWearFactor * wearMultiplier;
        }

        return new RaceResult
        {
            DriverName = driver.Name,
            TotalTime = totalTime,
            PitStops = pits
        };
    }

    private static TyreCompound GetCompound(TyreCompoundType type) => type switch
    {
        TyreCompoundType.Soft => TyreCompound.Soft,
        TyreCompoundType.Hard => TyreCompound.Hard,
        _ => TyreCompound.Medium
    };
}