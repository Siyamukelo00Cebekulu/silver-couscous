using GrandPrix.Core.Common;

namespace GrandPrix.Domain.Car;

public enum DevelopmentFocus
{
    Aerodynamics,
    Chassis,
    Reliability
}

public enum DevelopmentStage
{
    WindTunnel,
    Design,
    Build,
    Complete,
    Rejected
}

public sealed class CarDesign
{
    public double Downforce { get; set; } = 70;
    public double DragEfficiency { get; set; } = 70;
    public double Reliability { get; set; } = 75;
    public double ComponentLifeLaps { get; set; } = 650;
    public long CostCapSpentCents { get; set; } = 900_000_000;
    public const long CostCapLimitCents = 1_400_000_000;

    public double PaceFactor => Math.Max(0.90, 1.08 - Downforce * 0.00055 - DragEfficiency * 0.0003);
    public bool IsLegal => Downforce <= 100 && DragEfficiency <= 100 &&
        Reliability <= 100 && CostCapSpentCents <= CostCapLimitCents;
}

public sealed class DevelopmentProject
{
    public DevelopmentProject(DevelopmentFocus focus, Money budget, string description)
    {
        Focus = focus;
        Budget = budget;
        Description = description;
    }

    public DevelopmentFocus Focus { get; }
    public Money Budget { get; }
    public string Description { get; }
    public DevelopmentStage Stage { get; set; } = DevelopmentStage.WindTunnel;
    public double Progress { get; set; }
    public double DownforceDelta { get; set; }
    public double DragDelta { get; set; }
    public double ReliabilityDelta { get; set; }
    public double LifespanDeltaLaps { get; set; }
}

public sealed class DevelopmentStageResult
{
    public DevelopmentStage Stage { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Complete { get; set; }
    public bool Failed { get; set; }
}
