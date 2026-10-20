using System.Collections.Generic;
using GrandPrix.Core.Common;
using GrandPrix.Domain.Staff;

namespace GrandPrix.Domain.Team;

public sealed class Team
{
    public Team(EntityId id, string name, Money budget, Money monthlyBasePayroll)
    {
        Id = id;
        Name = name;
        Budget = budget;
        MonthlyBasePayroll = monthlyBasePayroll;
    }

    public EntityId Id { get; }
    public string Name { get; }
    public Money Budget { get; private set; }
    public Money MonthlyBasePayroll { get; }
    public List<Engineer> Engineers { get; } = new();
    public int ChampionshipPoints { get; private set; }

    public Money MonthlyPayroll
    {
        get
        {
            var engineerPayroll = 0L;
            foreach (var engineer in Engineers)
                engineerPayroll = checked(engineerPayroll + engineer.MonthlySalary.Cents);
            return new Money(checked(MonthlyBasePayroll.Cents + engineerPayroll));
        }
    }

    public void Deduct(Money amount)
    {
        if (amount.Cents < 0 || amount.Cents > Budget.Cents)
            throw new System.InvalidOperationException("Expense exceeds the available team budget.");
        Budget = new Money(Budget.Cents - amount.Cents);
    }

    public void AddFunds(Money amount)
    {
        if (amount.Cents < 0)
            throw new System.ArgumentOutOfRangeException(nameof(amount));
        Budget = new Money(checked(Budget.Cents + amount.Cents));
    }

    public void AddPoints(int points)
    {
        if (points < 0)
            throw new System.ArgumentOutOfRangeException(nameof(points));
        ChampionshipPoints = checked(ChampionshipPoints + points);
    }
}
