using System;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Simulation;
using GrandPrix.Domain.Calendar;
using GrandPrix.Domain.Drivers;
using GrandPrix.Domain.Race;

namespace GrandPrix.Domain.Race;

public class LapCalculator
{
    private readonly IRandom _rng;

    public LapCalculator(int seed)
        : this(new SeededRandom(seed).Fork("lap-times"))
    {
    }

    public LapCalculator(IRandom random)
    {
        _rng = random ?? throw new ArgumentNullException(nameof(random));
    }

    public double CalculateLapTime(
        Driver driver,
        TyreCompound tyre,
        double tyreWear,   // 0-100
        Track track,
        PaceMode pace)
    {
        // Driver pace: up to 4% faster at 100
        double skill = 1.0 - (driver.Pace / 100.0) * 0.04;

        // Tyre grip: higher grip = faster
        double grip = 1.0 / tyre.Grip;

        // Tyre wear: up to 12% slower at full wear
        double wear = 1.0 + (tyreWear / 100.0) * 0.12;

        // Pace mode
        double paceFactor = pace switch
        {
            PaceMode.Push => 0.985,
            PaceMode.Conserve => 1.015,
            _ => 1.0
        };

        // Consistency: lower consistency = more variance
        double varianceRange = (100 - driver.Consistency) / 100.0 * 0.02;
        double variance = 1.0 + (_rng.NextDouble() - 0.5) * varianceRange;

        return track.BaseLapTime * skill * grip * wear * paceFactor * variance;
    }
}