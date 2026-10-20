using GrandPrix.Core.Common;

namespace GrandPrix.Domain.Staff;

public sealed class Engineer
{
    public Engineer(EntityId id, string name, Money annualSalary, int contractYears, int skill = 70)
    {
        Id = id;
        Name = name;
        AnnualSalary = annualSalary;
        ContractYears = contractYears;
        Skill = skill;
    }

    public EntityId Id { get; }
    public string Name { get; }
    public Money AnnualSalary { get; }
    public int ContractYears { get; }
    public int Skill { get; }
    public Money MonthlySalary => new(AnnualSalary.Cents / 12);
}

public sealed class EngineerCandidate
{
    public EngineerCandidate(EntityId id, string name, Money askingAnnualSalary, int skill)
    {
        Id = id;
        Name = name;
        AskingAnnualSalary = askingAnnualSalary;
        Skill = skill;
    }

    public EntityId Id { get; }
    public string Name { get; }
    public Money AskingAnnualSalary { get; }
    public int Skill { get; }
}
