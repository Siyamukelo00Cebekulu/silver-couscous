using System;

namespace GrandPrix.Domain.Race;

public sealed class RaceWeekend
{
    public RaceWeekend(string circuitName)
    {
        CircuitName = circuitName;
    }

    public string CircuitName { get; }
    public IReadOnlyList<PracticeResult> PracticeResults { get; set; } = Array.Empty<PracticeResult>();
    public IReadOnlyList<QualifyingResult> QualifyingResults { get; set; } = Array.Empty<QualifyingResult>();
    public bool PracticeComplete { get; set; }
    public bool QualifyingComplete { get; set; }
    public bool RaceComplete { get; set; }
}
