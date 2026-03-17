using TgInfoBot;
using Xunit;

namespace TgInfoBot.Tests;

public class JplHorizonsClientTests
{
    // Minimal JPL-style response text with $$SOE / $$EOE markers
    private const string SampleResult = """
        **** header ****
        JDTDB, Calendar Date (TDB), ..., EclLon, EclLat,
        $$SOE
         2026-Jan-01 00:00, , , 268.6516112, -0.5700521,
         2026-Feb-26 00:00, , , 355.1234567, -1.2345678,
         2026-Mar-22 00:00, , ,  12.9876543,  0.1111111,
        $$EOE
        **** footer ****
        """;

    [Fact]
    public void ParseLongitudes_ValidInput_ReturnsCorrectRowCount()
    {
        var result = JplHorizonsClient.ParseLongitudes(SampleResult);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ParseLongitudes_ValidInput_ParsesDatesCorrectly()
    {
        var result = JplHorizonsClient.ParseLongitudes(SampleResult);
        Assert.Equal(new DateOnly(2026, 1,  1), result[0].date);
        Assert.Equal(new DateOnly(2026, 2, 26), result[1].date);
        Assert.Equal(new DateOnly(2026, 3, 22), result[2].date);
    }

    [Fact]
    public void ParseLongitudes_ValidInput_ParsesLongitudesCorrectly()
    {
        var result = JplHorizonsClient.ParseLongitudes(SampleResult);
        Assert.Equal(268.6516112, result[0].longitude, precision: 6);
        Assert.Equal(355.1234567, result[1].longitude, precision: 6);
        Assert.Equal( 12.9876543, result[2].longitude, precision: 6);
    }

    [Fact]
    public void ParseLongitudes_NoDataSection_ReturnsEmpty()
    {
        var noData = "some header\n$$SOE\n$$EOE\nsome footer";
        var result = JplHorizonsClient.ParseLongitudes(noData);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseLongitudes_NoSoeMarker_ReturnsEmpty()
    {
        var text = " 2026-Jan-01 00:00, , , 268.0, -0.5,\n 2026-Jan-02 00:00, , , 269.0, -0.6,";
        var result = JplHorizonsClient.ParseLongitudes(text);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseLongitudes_MalformedRows_SkippedGracefully()
    {
        var mixed = """
            $$SOE
             2026-Jan-01 00:00, , , 268.0, -0.5,
             not-a-date, , , 999.0, 0.0,
             GARBAGE LINE
             2026-Jan-02 00:00, , , 269.0, -0.6,
            $$EOE
            """;
        var result = JplHorizonsClient.ParseLongitudes(mixed);
        Assert.Equal(2, result.Count);
    }
}
