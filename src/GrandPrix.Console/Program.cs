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

var driver = new Driver
{
    Name = "Your Driver",
    Pace = 82,
    Consistency = 78,
    TyreManagement = 80
};

var strategies = new[]
{
    new StrategyPlan { StartingTyre = TyreCompoundType.Soft,   PitLap = 15, PitTyre = TyreCompoundType.Hard, Pace = PaceMode.Push },
    new StrategyPlan { StartingTyre = TyreCompoundType.Medium, PitLap = 25, PitTyre = TyreCompoundType.Medium, Pace = PaceMode.Normal },
    new StrategyPlan { StartingTyre = TyreCompoundType.Hard,   PitLap = 35, PitTyre = TyreCompoundType.Soft, Pace = PaceMode.Conserve },
};

Console.WriteLine($"=== {track.Name} — {track.Laps} laps ===");
Console.WriteLine($"Driver: {driver.Name}\n");

var sim = new RaceSimulator(track, seed: 42);

foreach (var plan in strategies)
{
    var result = sim.Simulate(driver, plan);
    var minutes = (int)(result.TotalTime / 60);
    var seconds = result.TotalTime % 60;
    Console.WriteLine($"Plan: Start {plan.StartingTyre}, Pit L{plan.PitLap} → {plan.PitTyre}, Pace {plan.Pace}");
    Console.WriteLine($"  Total: {minutes}m {seconds:F3}s | Pit stops: {result.PitStops}\n");
}