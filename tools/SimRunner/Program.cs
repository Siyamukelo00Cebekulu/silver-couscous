using GrandPrix.Domain.Calendar;
using GrandPrix.Domain.Drivers;
using GrandPrix.Domain.Race;

if (args.Length > 0 && !int.TryParse(args[0], out _))
{
    Console.Error.WriteLine("Usage: SimRunner [seed]");
    return 1;
}

var seed = args.Length == 0 ? 42 : int.Parse(args[0]);
var track = new Track
{
    Name = "Simulation Track",
    BaseLapTime = 90,
    Laps = 10,
    TyreWearFactor = 1,
    PitLossTime = 20
};
var driver = new Driver { Name = "Simulation Driver" };
var result = new RaceSimulator(track, seed).Simulate(driver, new StrategyPlan
{
    StartingTyre = TyreCompoundType.Medium,
    PitLap = track.Laps / 2,
    PitTyre = TyreCompoundType.Hard
});

Console.WriteLine($"{result.DriverName}: {result.TotalTime:F3}s, {result.PitStops} pit stop(s)");
return 0;
