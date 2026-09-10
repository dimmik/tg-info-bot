using TgInfoBot;
using Xunit;

namespace TgInfoBot.Tests;

public class InfoByDateTests
{
    // Config with command "cmd", keywords "mercury"/"retro", Bad emotion, 3-day alert.
    // Static date ranges: 2024-01-01:2024-01-31  and  2024-06-01:2024-06-30
    private const string ValidConfig =
        "[cmd#mercury#retro:Mercury Retrograde:Mercury Retrograde:Bad:3]," +
        "2024-01-01:2024-01-31;2024-06-01:2024-06-30;";

    // Anchor dates used across tests
    private static readonly DateTimeOffset BeforeAll       = new(2023,  6,  1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset InFirstRange    = new(2024,  1, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BetweenRanges   = new(2024,  3, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AlmostNextRange = new(2024,  5, 30, 0, 0, 0, TimeSpan.Zero); // 2 days before 2024-06-01
    private static readonly DateTimeOffset FarFromNext     = new(2024,  3,  1, 0, 0, 0, TimeSpan.Zero); // ~90 days before 2024-06-01
    private static readonly DateTimeOffset AfterAll        = new(2025,  1,  1, 0, 0, 0, TimeSpan.Zero);

    // --- Constructor parsing ---

    [Fact]
    public void Constructor_ValidString_DoesNotThrow()
    {
        var sut = new InfoByDate(ValidConfig);
        Assert.NotNull(sut);
    }

    [Fact]
    public void Constructor_EmptyString_Throws()
    {
        Assert.Throws<ArgumentException>(() => new InfoByDate(""));
    }

    [Fact]
    public void Constructor_WhitespaceString_Throws()
    {
        Assert.Throws<ArgumentException>(() => new InfoByDate("   "));
    }

    [Fact]
    public void Constructor_MissingComma_Throws()
    {
        Assert.Throws<FormatException>(() => new InfoByDate("[cmd#kw:Desc:DescFrom:Bad:0]"));
    }

    [Fact]
    public void Constructor_TooFewMetadataFields_Throws()
    {
        Assert.Throws<FormatException>(() => new InfoByDate("[cmd#kw:Desc:DescFrom],2024-01-01:2024-01-31;"));
    }

    [Fact]
    public void Constructor_InvalidDateFormat_Throws()
    {
        Assert.Throws<FormatException>(() => new InfoByDate("[cmd#kw:Desc:DescFrom:Bad:0],notadate:2024-01-31;"));
    }

    [Fact]
    public void Constructor_StartAfterEnd_Throws()
    {
        Assert.Throws<FormatException>(() => new InfoByDate("[cmd#kw:Desc:DescFrom:Bad:0],2024-01-31:2024-01-01;"));
    }

    [Fact]
    public void Constructor_NoDates_Throws()
    {
        Assert.Throws<FormatException>(() => new InfoByDate("[cmd#kw:Desc:DescFrom:Bad:0],   ;   ;"));
    }

    // --- Accept / keyword matching ---

    [Fact]
    public void Accept_MatchingKeyword_ReturnsTrue()
    {
        var sut = new InfoByDate(ValidConfig);
        Assert.True(sut.Accept("I heard mercury is going crazy"));
    }

    [Fact]
    public void Accept_MatchingKeywordCaseInsensitive_ReturnsTrue()
    {
        var sut = new InfoByDate(ValidConfig);
        Assert.True(sut.Accept("MERCURY goes RETRO"));
    }

    [Fact]
    public void Accept_NoKeywordMatch_ReturnsFalse()
    {
        var sut = new InfoByDate(ValidConfig);
        Assert.False(sut.Accept("Nothing special today"));
    }

    [Fact]
    public void Accept_EmptyString_ReturnsFalse()
    {
        var sut = new InfoByDate(ValidConfig);
        Assert.False(sut.Accept(""));
    }

    // --- Accept: normalization / obfuscation resistance ---

    // Config with a Russian *stem* keyword "меркури" so any declension matches.
    private const string RuConfig =
        "[cmd#меркури:Меркурий ретроградный:ретроградного Меркурия:Bad:3]," +
        "2024-01-01:2024-01-31;";

    [Theory]
    [InlineData("меркурий сегодня ретроградный")]      // nominative
    [InlineData("может и правда дело в меркурии?")]     // prepositional case — original miss
    [InlineData("может и правда дело в м е р к у р и и?")] // spaced-out letters
    [InlineData("это всё м.е.р.к.у.р.и.й виноват")]       // dotted obfuscation
    [InlineData("МеРкУрИй ретроградит")]                  // mixed case
    [InlineData("опять ёлки и мёркурий")]                 // ё handled via homoglyph map
    public void Accept_ObfuscatedRussianMention_ReturnsTrue(string message)
    {
        var sut = new InfoByDate(RuConfig);
        Assert.True(sut.Accept(message));
    }

    [Fact]
    public void Accept_LatinHomoglyphMention_ReturnsTrue()
    {
        // "mеркурий" — Latin 'm' + Cyrillic tail, a classic homoglyph trick.
        var sut = new InfoByDate(RuConfig);
        Assert.True(sut.Accept("опять этот mеркурий"));
    }

    [Fact]
    public void Accept_UnrelatedRussianText_ReturnsFalse()
    {
        var sut = new InfoByDate(RuConfig);
        Assert.False(sut.Accept("сегодня отличная погода, идём в кино"));
    }

    // --- GetInfo: active retrograde ---

    [Fact]
    public void GetInfo_InRetrograde_ContainsInProgressText()
    {
        var sut = new InfoByDate(ValidConfig);
        var result = sut.GetInfo(InFirstRange);
        Assert.Contains("в процессе", result);
    }

    [Fact]
    public void GetInfo_InRetrograde_BadEmotion_ContainsPechal()
    {
        var sut = new InfoByDate(ValidConfig);
        var result = sut.GetInfo(InFirstRange);
        Assert.Contains("Печаль", result);
    }

    [Fact]
    public void GetInfo_InRetrograde_GoodEmotion_ContainsOtlichno()
    {
        var goodConfig = "[cmd#kw:Good Event:Good Events:Good:0],2024-01-01:2024-01-31;";
        var sut = new InfoByDate(goodConfig);
        var result = sut.GetInfo(InFirstRange);
        Assert.Contains("Отлично", result);
    }

    // --- GetInfo: no active retrograde ---

    [Fact]
    public void GetInfo_BeforeAll_ShowsNextPeriod()
    {
        var sut = new InfoByDate(ValidConfig);
        var result = sut.GetInfo(BeforeAll);
        Assert.Contains("следующего", result);
        Assert.DoesNotContain("в процессе", result);
    }

    [Fact]
    public void GetInfo_BetweenRanges_ShowsBothNextAndPrevious()
    {
        var sut = new InfoByDate(ValidConfig);
        var result = sut.GetInfo(BetweenRanges);
        Assert.Contains("следующего", result);
        Assert.Contains("предыдущего", result);
    }

    [Fact]
    public void GetInfo_AfterAll_ShowsPreviousPeriod()
    {
        var sut = new InfoByDate(ValidConfig);
        var result = sut.GetInfo(AfterAll);
        Assert.Contains("предыдущего", result);
        Assert.DoesNotContain("следующего", result);
    }

    // --- AlertTimeBefore ---

    [Fact]
    public void GetInfo_WithinAlertThreshold_ShowsWarning()
    {
        var sut = new InfoByDate(ValidConfig); // alert = 3 days
        // AlmostNextRange is 2024-05-30, next period starts 2024-06-01 (2 days away)
        var result = sut.GetInfo(AlmostNextRange);
        Assert.Contains("Внимание", result);
    }

    [Fact]
    public void GetInfo_OutsideAlertThreshold_NoWarning()
    {
        var sut = new InfoByDate(ValidConfig); // alert = 3 days
        // FarFromNext is ~90 days before the next period
        var result = sut.GetInfo(FarFromNext);
        Assert.DoesNotContain("Внимание", result);
    }

    // --- LiveProvider ---

    [Fact]
    public void GetInfo_LiveProviderWithData_UsesLiveRanges()
    {
        var sut = new InfoByDate(ValidConfig);
        // Live range covers 2025-03 — not in the static config
        var liveFrom = new DateTimeOffset(2025, 3,  1, 0, 0, 0, TimeSpan.Zero);
        var liveTo   = new DateTimeOffset(2025, 3, 31, 23, 59, 59, TimeSpan.Zero);
        sut.LiveProvider = new FakeProvider([(liveFrom, liveTo)]);

        var result = sut.GetInfo(new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero));
        Assert.Contains("в процессе", result);
    }

    [Fact]
    public void GetInfo_LiveProviderEmpty_FallsBackToStaticDates()
    {
        var sut = new InfoByDate(ValidConfig);
        sut.LiveProvider = new FakeProvider([]); // HasData = false

        // InFirstRange is inside static config — should still return "в процессе"
        var result = sut.GetInfo(InFirstRange);
        Assert.Contains("в процессе", result);
    }

    [Fact]
    public void GetInfo_LiveProviderWithData_MessageContainsDatasourceNote()
    {
        var sut = new InfoByDate(ValidConfig);
        var liveFrom = new DateTimeOffset(2025, 3,  1, 0, 0, 0, TimeSpan.Zero);
        var liveTo   = new DateTimeOffset(2025, 3, 31, 23, 59, 59, TimeSpan.Zero);
        sut.LiveProvider = new FakeProvider([(liveFrom, liveTo)]);

        var result = sut.GetInfo(new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero));
        Assert.Contains("JPL Horizons", result);
    }

    // --- Stub ---

    private sealed class FakeProvider : IMercuryRetrogradeProvider
    {
        private readonly (DateTimeOffset from, DateTimeOffset to)[] _ranges;
        public FakeProvider((DateTimeOffset from, DateTimeOffset to)[] ranges) => _ranges = ranges;
        public (DateTimeOffset from, DateTimeOffset to)[] Ranges => _ranges;
        public DateTimeOffset LastUpdated => DateTimeOffset.UtcNow;
        public bool HasData => _ranges.Length > 0;
    }
}
