using GrandPrix.Domain.Staff;
using GrandPrix.Domain.Team;

namespace GrandPrix.AI;

public interface ITeamPrincipalPolicy
{
    HireEngineerCommand? ChooseAction(
        Team team,
        IReadOnlyList<EngineerCandidate> market);
}
