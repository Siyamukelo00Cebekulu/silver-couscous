namespace GrandPrix.Domain.Race;

public enum PaceMode { Conserve, Normal, Push }

public class StrategyPlan
{
    public TyreCompoundType StartingTyre { get; set; } = TyreCompoundType.Medium;
    public int PitLap { get; set; } = 20;
    public TyreCompoundType PitTyre { get; set; } = TyreCompoundType.Hard;
    public PaceMode Pace { get; set; } = PaceMode.Normal;
}