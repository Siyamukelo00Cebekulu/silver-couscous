namespace GrandPrix.Core.Models;

public enum TyreCompoundType { Soft, Medium, Hard, Intermediate, Wet }

public class TyreCompound
{
    public TyreCompoundType Type { get; set; }
    public double Grip { get; set; }        // 1.0 = baseline
    public double WearRate { get; set; }    // wear per lap (0-100 scale)
    public double OptimalTempLow { get; set; }
    public double OptimalTempHigh { get; set; }

    public static TyreCompound Soft => new()
    {
        Type = TyreCompoundType.Soft,
        Grip = 1.03,
        WearRate = 3.2,
        OptimalTempLow = 90,
        OptimalTempHigh = 110
    };

    public static TyreCompound Medium => new()
    {
        Type = TyreCompoundType.Medium,
        Grip = 1.00,
        WearRate = 2.2,
        OptimalTempLow = 80,
        OptimalTempHigh = 100
    };

    public static TyreCompound Hard => new()
    {
        Type = TyreCompoundType.Hard,
        Grip = 0.97,
        WearRate = 1.5,
        OptimalTempLow = 70,
        OptimalTempHigh = 90
    };
}