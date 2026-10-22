using GrandPrix.Domain.Race;

namespace GrandPrix.AI;

public sealed class ConservativeAiRaceStrategyPolicy : IAiRaceStrategyPolicy
{
    public string PolicyId => "conservative-race-v1";

    public IReadOnlyList<IRaceCommand> ChooseCommands(RaceSetup setup, RaceSnapshot snapshot)
    {
        if (setup is null)
            throw new ArgumentNullException(nameof(setup));
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));

        var aiCars = new HashSet<GrandPrix.Core.Common.EntityId>(
            setup.Grid.Where(car => car.IsAi).Select(car => car.CarId));
        var commands = new List<IRaceCommand>();
        foreach (var car in snapshot.Cars)
        {
            if (!aiCars.Contains(car.CarId) || car.Retired ||
                car.LapsCompleted >= setup.TotalLaps)
                continue;

            var nextLap = car.LapsCompleted + 1;
            if (nextLap == Math.Max(2, setup.TotalLaps / 2))
            {
                var nextTyre = car.Tyre == RaceTyreCompound.Soft
                    ? RaceTyreCompound.Hard
                    : RaceTyreCompound.Soft;
                commands.Add(new BoxThisLapCommand(car.CarId, nextTyre));
            }

            var mode = car.TyreWear >= 0.55
                ? DriverMode.SaveTyres
                : nextLap > setup.TotalLaps * 0.75
                    ? DriverMode.Push
                    : DriverMode.Standard;
            commands.Add(new SetDriverModeCommand(car.CarId, mode));
        }

        return commands;
    }
}
