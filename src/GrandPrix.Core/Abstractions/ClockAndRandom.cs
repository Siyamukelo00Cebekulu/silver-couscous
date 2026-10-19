using System;

namespace GrandPrix.Core.Abstractions;

public interface IClock
{
    DateTime Today { get; }
    event Action<DateTime>? DayAdvanced;
    void AdvanceDays(int days);
}

public interface IRandom
{
    int Seed { get; }
    int Next(int minInclusive, int maxExclusive);
    double NextDouble();
    double NextGaussian(double mean, double stdDev);
    bool Chance(double probability);
    IRandom Fork(string streamName);
}
