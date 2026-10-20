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
    public PracticeFocus PracticeFocus { get; set; } = PracticeFocus.Setup;
    public double SetupConfidence { get; set; }
    public double RacePaceBonus { get; set; }
    public TyreCompoundType QualifyingTyre { get; set; } = TyreCompoundType.Soft;
    public QualiRisk QualifyingRisk { get; set; } = QualiRisk.Normal;
    public int QualifyingRuns { get; set; } = 2;
    public bool PracticeComplete { get; set; }
    public bool QualifyingComplete { get; set; }
    public bool RaceComplete { get; set; }
}

public sealed class RaceStrategyAdvice
{
    public string Message { get; set; } = string.Empty;
    public RaceTyreCompound SuggestedTyre { get; set; }
    public bool RecommendPit { get; set; }
}
