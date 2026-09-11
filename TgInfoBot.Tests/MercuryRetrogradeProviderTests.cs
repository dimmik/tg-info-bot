using TgInfoBot;
using Xunit;

namespace TgInfoBot.Tests;

public class MercuryRetrogradeProviderTests
{
    // Helpers
    private static (DateOnly date, double longitude) Row(int year, int month, int day, double lon)
        => (new DateOnly(year, month, day), lon);

    private static DateOnly From(DateTimeOffset dto) => DateOnly.FromDateTime(dto.DateTime);

    [Fact]
    public void ComputeRetrogradeRanges_EmptyInput_ReturnsEmpty()
    {
        var result = MercuryRetrogradeProvider.ComputeRetrogradeRanges([]);
        Assert.Empty(result);
    }

    [Fact]
    public void ComputeRetrogradeRanges_AlwaysPrograde_ReturnsEmpty()
    {
        var rows = new List<(DateOnly, double)>
        {
            Row(2024, 1, 1, 100.0),
            Row(2024, 1, 2, 101.0),
            Row(2024, 1, 3, 102.0),
        };
        var result = MercuryRetrogradeProvider.ComputeRetrogradeRanges(rows);
        Assert.Empty(result);
    }

    [Fact]
    public void ComputeRetrogradeRanges_SinglePeriod_DetectedCorrectly()
    {
        // Prograde -> retrograde -> prograde
        var rows = new List<(DateOnly, double)>
        {
            Row(2024, 1, 1, 100.0),
            Row(2024, 1, 2, 101.0), // prograde
            Row(2024, 1, 3, 102.0), // prograde; retrograde starts here (prev = this day)
            Row(2024, 1, 4, 101.5), // delta < 0  -> inRetro=true, retroStart = 2024-01-03
            Row(2024, 1, 5, 101.0), // retrograde
            Row(2024, 1, 6, 101.8), // delta >= 0 -> period ends, endDate = 2024-01-05
            Row(2024, 1, 7, 102.5),
        };

        var result = MercuryRetrogradeProvider.ComputeRetrogradeRanges(rows);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2024, 1, 3), From(result[0].from));
        Assert.Equal(new DateOnly(2024, 1, 5), From(result[0].to));
    }

    [Fact]
    public void ComputeRetrogradeRanges_MultiplePeriods_AllDetected()
    {
        var rows = new List<(DateOnly, double)>
        {
            Row(2024, 1,  1, 100.0),
            Row(2024, 1,  2, 101.0),
            Row(2024, 1,  3, 100.5), // retro 1 starts (retroStart = Jan 2)
            Row(2024, 1,  4, 101.2), // retro 1 ends at Jan 3
            Row(2024, 1,  5, 102.0),
            Row(2024, 1,  6, 101.8), // retro 2 starts (retroStart = Jan 5)
            Row(2024, 1,  7, 102.5), // retro 2 ends at Jan 6
        };

        var result = MercuryRetrogradeProvider.ComputeRetrogradeRanges(rows);

        Assert.Equal(2, result.Length);
        Assert.Equal(new DateOnly(2024, 1, 2), From(result[0].from));
        Assert.Equal(new DateOnly(2024, 1, 3), From(result[0].to));
        Assert.Equal(new DateOnly(2024, 1, 5), From(result[1].from));
        Assert.Equal(new DateOnly(2024, 1, 6), From(result[1].to));
    }

    [Fact]
    public void ComputeRetrogradeRanges_WrapAround360_NotTreatedAsRetrograde()
    {
        // Longitude wraps 359 -> 1 (delta = -358, normalised to +2 -> prograde)
        var rows = new List<(DateOnly, double)>
        {
            Row(2024, 1, 1, 358.0),
            Row(2024, 1, 2, 359.5),
            Row(2024, 1, 3,   1.0), // apparent delta = -358.5, normalised = +1.5 -> prograde
            Row(2024, 1, 4,   2.5),
        };

        var result = MercuryRetrogradeProvider.ComputeRetrogradeRanges(rows);
        Assert.Empty(result);
    }

    [Fact]
    public void ComputeRetrogradeRanges_OngoingRetrograde_IncludedWithLastDateAsEnd()
    {
        // Retrograde begins but data ends while still retrograde
        var rows = new List<(DateOnly, double)>
        {
            Row(2024, 1, 1, 100.0),
            Row(2024, 1, 2, 101.0),
            Row(2024, 1, 3, 100.5), // retro starts; retroStart = Jan 2
            Row(2024, 1, 4,  99.8), // still retro - no end in data
        };

        var result = MercuryRetrogradeProvider.ComputeRetrogradeRanges(rows);

        Assert.Single(result);
        Assert.Equal(new DateOnly(2024, 1, 2), From(result[0].from));
        Assert.Equal(new DateOnly(2024, 1, 4), From(result[0].to)); // last data point
    }

    [Fact]
    public void ComputeRetrogradeRanges_EndDayIncludesFullDay()
    {
        var rows = new List<(DateOnly, double)>
        {
            Row(2024, 1, 1, 100.0),
            Row(2024, 1, 2, 101.0),
            Row(2024, 1, 3, 100.5),
            Row(2024, 1, 4, 101.2),
        };

        var result = MercuryRetrogradeProvider.ComputeRetrogradeRanges(rows);
        Assert.Single(result);
        // to should be 23:59:59 of the end date
        Assert.Equal(TimeOnly.MaxValue.ToTimeSpan().Hours,   result[0].to.Hour);
        Assert.Equal(TimeOnly.MaxValue.ToTimeSpan().Minutes, result[0].to.Minute);
    }
}
