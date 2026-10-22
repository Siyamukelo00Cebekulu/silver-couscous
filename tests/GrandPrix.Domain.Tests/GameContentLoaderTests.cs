using GrandPrix.Data;
using GrandPrix.Series.F1;

namespace GrandPrix.Domain.Tests;

public sealed class GameContentLoaderTests
{
    [Fact]
    public void EmbeddedSeasonContentIsTypedAndValid()
    {
        var content = GameContentLoader.LoadEmbedded();

        Assert.Equal(1, content.SchemaVersion);
        Assert.Equal("apex-gp", content.PlayerTeamId);
        Assert.Equal(5, content.Teams.Count);
        Assert.Equal(10, content.Drivers.Count);
        Assert.Equal(5, content.Circuits.Count);
        Assert.Equal(4, content.Engineers.Count);
        Assert.Empty(GameContentLoader.Validate(content));
    }

    [Fact]
    public void ValidationReportsDuplicateIdsAndInvalidDriverRatings()
    {
        var content = GameContentLoader.LoadEmbedded();
        content.Teams[1].Id = content.Teams[0].Id;
        content.Drivers[0].Pace = 101;

        var errors = GameContentLoader.Validate(content);

        Assert.Contains(errors, error => error.Contains("Duplicate team ID", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Ratings", StringComparison.Ordinal));
    }

    [Fact]
    public void F1SeriesProviderOwnsCalendarScoringAndCostCapRules()
    {
        var rules = new F1RulesProvider();

        Assert.Equal("f1", rules.SeriesId);
        Assert.Equal(1_400_000_000, rules.CostCapLimitCents);
        Assert.Equal(5, rules.Calendar.Count);
        Assert.Equal("caspian-park", rules.Calendar[0].CircuitId);
        Assert.Equal(35, rules.Calendar[0].DaysAfterSeasonStart);
        Assert.Equal(25, rules.PointsForPosition(1));
        Assert.Equal(1, rules.PointsForPosition(10));
        Assert.Equal(0, rules.PointsForPosition(11));
        Assert.Equal(200_000_000, rules.PrizeCentsForPosition(1));
        Assert.Equal(0, rules.PrizeCentsForPosition(11));
    }

    [Fact]
    public void CalendarAdvanceRunsRivalPrincipalsThroughTheHiringCommandHandler()
    {
        var session = new GrandPrix.Application.GameSession();

        session.AdvanceDays(1);

        Assert.All(session.Teams.Skip(1), team => Assert.Single(team.Engineers));
        Assert.Contains(session.GetInbox(), item =>
            item.Headline.Contains("joined Aurora Motorsport", StringComparison.Ordinal));
    }
}
