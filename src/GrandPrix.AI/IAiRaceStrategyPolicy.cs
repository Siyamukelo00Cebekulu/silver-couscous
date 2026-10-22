using GrandPrix.Domain.Race;

namespace GrandPrix.AI;

public interface IAiRaceStrategyPolicy
{
    string PolicyId { get; }
    IReadOnlyList<IRaceCommand> ChooseCommands(RaceSetup setup, RaceSnapshot snapshot);
}
