namespace GrandPrix.Series.Abstractions;

public interface ISeriesRulesProvider
{
    string SeriesId { get; }
    long CostCapLimitCents { get; }
    IReadOnlyList<SeriesRoundDefinition> Calendar { get; }
    int PointsForPosition(int position);
    long PrizeCentsForPosition(int position);
}

public sealed class SeriesRoundDefinition
{
    public SeriesRoundDefinition(string circuitId, int daysAfterSeasonStart)
    {
        CircuitId = circuitId;
        DaysAfterSeasonStart = daysAfterSeasonStart;
    }

    public string CircuitId { get; }
    public int DaysAfterSeasonStart { get; }
}
