using GrandPrix.Series.Abstractions;

namespace GrandPrix.Series.F1;

public sealed class F1RulesProvider : ISeriesRulesProvider
{
    private static readonly int[] Points =
        { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 };
    private static readonly long[] PrizeCents =
    {
        200_000_000, 150_000_000, 120_000_000, 95_000_000, 75_000_000,
        60_000_000, 45_000_000, 30_000_000, 20_000_000, 10_000_000
    };
    private static readonly IReadOnlyList<SeriesRoundDefinition> Rounds =
        Array.AsReadOnly(new[]
        {
            new SeriesRoundDefinition("caspian-park", 35),
            new SeriesRoundDefinition("silverstone", 70),
            new SeriesRoundDefinition("monza", 105),
            new SeriesRoundDefinition("suzuka", 140),
            new SeriesRoundDefinition("marina-bay", 175)
        });

    public string SeriesId => "f1";
    public long CostCapLimitCents => 1_400_000_000;
    public IReadOnlyList<SeriesRoundDefinition> Calendar => Rounds;

    public int PointsForPosition(int position) =>
        position > 0 && position <= Points.Length ? Points[position - 1] : 0;

    public long PrizeCentsForPosition(int position) =>
        position > 0 && position <= PrizeCents.Length ? PrizeCents[position - 1] : 0;
}
