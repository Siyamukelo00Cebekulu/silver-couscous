using System;
using GrandPrix.Core.Abstractions;

namespace GrandPrix.Core.Simulation;

public sealed class ManualGameClock : IClock
{
    public ManualGameClock(DateTime startDate)
    {
        Today = startDate.Date;
    }

    public DateTime Today { get; private set; }
    public event Action<DateTime>? DayAdvanced;

    public void AdvanceDays(int days)
    {
        if (days <= 0)
            throw new ArgumentOutOfRangeException(nameof(days), "Days advanced must be positive.");

        for (var day = 0; day < days; day++)
        {
            Today = Today.AddDays(1);
            DayAdvanced?.Invoke(Today);
        }
    }
}
