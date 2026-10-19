using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Common;

namespace GrandPrix.Domain.Staff;

public sealed class HireEngineerCommand : ICommand
{
    public HireEngineerCommand(
        EntityId issuer,
        EntityId candidateId,
        Money offeredSalary,
        int contractYears)
    {
        Issuer = issuer;
        CandidateId = candidateId;
        OfferedSalary = offeredSalary;
        ContractYears = contractYears;
    }

    public EntityId Issuer { get; }
    public EntityId CandidateId { get; }
    public Money OfferedSalary { get; }
    public int ContractYears { get; }
}
