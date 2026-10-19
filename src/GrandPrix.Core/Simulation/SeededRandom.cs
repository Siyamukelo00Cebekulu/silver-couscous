using System;
using GrandPrix.Core.Abstractions;

namespace GrandPrix.Core.Simulation;

public sealed class SeededRandom : IRandom
{
    private const ulong StateIncrement = 0x9E3779B97F4A7C15UL;
    private const ulong MixMultiplier1 = 0xBF58476D1CE4E5B9UL;
    private const ulong MixMultiplier2 = 0x94D049BB133111EBUL;
    private readonly ulong _rootSeed;
    private ulong _state;

    public SeededRandom(int seed)
        : this(unchecked((ulong)(long)seed), seed)
    {
    }

    private SeededRandom(ulong rootSeed, int seed)
    {
        _rootSeed = rootSeed;
        _state = rootSeed;
        Seed = seed;
    }

    public int Seed { get; }

    public int Next(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "The maximum must be greater than the minimum.");

        var range = (ulong)((long)maxExclusive - minInclusive);
        var rejectionThreshold = unchecked(0UL - range) % range;
        ulong value;
        do
        {
            value = NextUInt64();
        }
        while (value < rejectionThreshold);

        return (int)(minInclusive + (long)(value % range));
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    public double NextGaussian(double mean, double stdDev)
    {
        if (double.IsNaN(mean) || double.IsInfinity(mean))
            throw new ArgumentOutOfRangeException(nameof(mean));
        if (double.IsNaN(stdDev) || double.IsInfinity(stdDev) || stdDev < 0)
            throw new ArgumentOutOfRangeException(nameof(stdDev));
        if (stdDev == 0)
            return mean;

        double first;
        do
        {
            first = NextDouble();
        }
        while (first == 0);

        var second = NextDouble();
        var standardNormal = Math.Sqrt(-2 * Math.Log(first)) * Math.Cos(2 * Math.PI * second);
        return mean + standardNormal * stdDev;
    }

    public bool Chance(double probability)
    {
        if (double.IsNaN(probability) || probability < 0 || probability > 1)
            throw new ArgumentOutOfRangeException(nameof(probability), "Probability must be between zero and one.");
        if (probability == 0)
            return false;
        if (probability == 1)
            return true;

        return NextDouble() < probability;
    }

    public IRandom Fork(string streamName)
    {
        if (string.IsNullOrWhiteSpace(streamName))
            throw new ArgumentException("A stream name is required.", nameof(streamName));

        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var nameHash = offset;
        foreach (var character in streamName)
            nameHash = (nameHash ^ character) * prime;

        var childRoot = Mix(_rootSeed ^ nameHash);
        return new SeededRandom(childRoot, unchecked((int)childRoot));
    }

    private ulong NextUInt64()
    {
        _state += StateIncrement;
        return Mix(_state);
    }

    private static ulong Mix(ulong value)
    {
        value = (value ^ (value >> 30)) * MixMultiplier1;
        value = (value ^ (value >> 27)) * MixMultiplier2;
        return value ^ (value >> 31);
    }
}
