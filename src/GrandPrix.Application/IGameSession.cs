using System.Collections.Generic;
using System;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Common;

namespace GrandPrix.Application;

public interface IGameSession
{
    CommandResult Execute(ICommand command);
    void AdvanceDay();
    DashboardViewModel GetDashboard();
    IReadOnlyList<InboxItemViewModel> GetInbox();
}

public sealed class DashboardViewModel
{
    public string TeamName { get; set; } = string.Empty;
    public DateTime Today { get; set; }
    public string NextEvent { get; set; } = string.Empty;
    public long BudgetRemainingCents { get; set; }
    public int ChampionshipPosition { get; set; }
}

public sealed class InboxItemViewModel
{
    public DateTime Date { get; set; }
    public string Headline { get; set; } = string.Empty;
    public bool Unread { get; set; }
}
