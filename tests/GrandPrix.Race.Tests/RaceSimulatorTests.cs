using GrandPrix.Domain.Calendar;
using GrandPrix.Domain.Drivers;
using GrandPrix.Domain.Race;

namespace GrandPrix.Race.Tests;

public class RaceSimulatorTests
{
    [Fact]
    public void SameSeedProducesSameRaceResult()
    {
        var track = new Track
        {
            Name = "Test Track",
            BaseLapTime = 90,
            Laps = 5,
            TyreWearFactor = 1,
            PitLossTime = 20
        };
        var driver = new Driver { Name = "Test Driver" };
        var plan = new StrategyPlan
        {
            StartingTyre = TyreCompoundType.Medium,
            PitLap = 3,
            PitTyre = TyreCompoundType.Hard
        };

        var first = new RaceSimulator(track, seed: 7).Simulate(driver, plan);
        var second = new RaceSimulator(track, seed: 7).Simulate(driver, plan);

        Assert.Equal(first.TotalTime, second.TotalTime);
        Assert.Equal(first.PitStops, second.PitStops);
    }
}
