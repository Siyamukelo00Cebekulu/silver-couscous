using System;
using GrandPrix.Core.Common;

namespace GrandPrix.Domain.Facilities;

public sealed class TeamFacilities
{
    public int WindTunnelLevel { get; private set; } = 1;
    public int SimulatorLevel { get; private set; } = 1;
    public int FactoryLevel { get; private set; } = 1;

    public int ResearchBonus => WindTunnelLevel * 3 + SimulatorLevel;
    public int PracticeBonus => SimulatorLevel * 2;
    public int BuildQualityBonus => FactoryLevel * 2;

    public Money UpgradeCost(string facility)
    {
        var level = GetLevel(facility);
        return new Money(checked((long)(level + 1) * (level + 1) * 5_000_000));
    }

    public void Upgrade(string facility)
    {
        switch (facility.Trim().ToLowerInvariant())
        {
            case "wind tunnel":
            case "wind":
                WindTunnelLevel = NextLevel(WindTunnelLevel);
                break;
            case "simulator":
                SimulatorLevel = NextLevel(SimulatorLevel);
                break;
            case "factory":
                FactoryLevel = NextLevel(FactoryLevel);
                break;
            default:
                throw new ArgumentException("Choose wind, simulator, or factory.", nameof(facility));
        }
    }

    private int GetLevel(string facility) => facility.Trim().ToLowerInvariant() switch
    {
        "wind tunnel" or "wind" => WindTunnelLevel,
        "simulator" => SimulatorLevel,
        "factory" => FactoryLevel,
        _ => throw new ArgumentException("Choose wind, simulator, or factory.", nameof(facility))
    };

    private static int NextLevel(int level)
    {
        if (level >= 5)
            throw new InvalidOperationException("Facilities cannot be upgraded beyond level 5.");
        return level + 1;
    }
}
