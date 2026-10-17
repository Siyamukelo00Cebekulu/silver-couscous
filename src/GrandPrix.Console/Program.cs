using System;
using GrandPrix.Core.Models;
using GrandPrix.Core.Simulation;

var track = new Track
{
    Name = "Silverstone",
    BaseLapTime = 88.0,
    Laps = 52,
    TyreWearFactor = 1.2,
    PitLossTime = 22.0
};


var weather = Weather.Dry;

var entries = new[]
{
    new WeekendEntry
    {
        Driver = new Driver { Name = "You",      Pace = 70, Consistency = 90, TyreManagement = 0 },
        IsPlayer = true
    },
    new WeekendEntry { Driver = new Driver { Name = "Rival A", Pace = 85, Consistency = 82, TyreManagement = 75 } },
    new WeekendEntry { Driver = new Driver { Name = "Rival B", Pace = 80, Consistency = 88, TyreManagement = 84 } },
    new WeekendEntry { Driver = new Driver { Name = "Rival C", Pace = 78, Consistency = 75, TyreManagement = 79 } }
};

var weekend = new WeekendState(track, weather, entries, seed: 42);


// --- PRACTICE ---
Console.WriteLine("=== PRACTICE ===");
var practice = weekend.RunPractice(new PracticePlan { Focus = PracticeFocus.Setup });
Console.WriteLine(practice.Report);
Console.WriteLine();

// --- QUALIFYING ---
Console.WriteLine("=== QUALIFYING ===");
var quali = weekend.RunQualifying(new QualifyingPlan
{
    Tyre = TyreCompoundType.Soft,
    Risk = QualiRisk.Push,
    Runs = 2
});
foreach (var r in quali)
    Console.WriteLine($"  P{r.GridPosition}  {r.DriverName,-10} {r.BestLapTime:F3}s");
Console.WriteLine();

// --- RACE ---
Console.WriteLine("=== RACE ===");
var race = weekend.RunRace(new StrategyPlan
{
    StartingTyre = TyreCompoundType.Hard,
    PitLap = 25,
    PitTyre = TyreCompoundType.Soft,
    Pace = PaceMode.Push
});

for (int i = 0; i < race.Count; i++)
{
    var r = race[i];
    int mins = (int)(r.TotalTime / 60);
    double secs = r.TotalTime % 60;
    Console.WriteLine($"  P{i + 1}  {r.DriverName,-10} {mins}m {secs:F3}s  ({r.PitStops} stop{(r.PitStops == 1 ? "" : "s")})");
}