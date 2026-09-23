using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TgInfoBot;
using TgInfoBot.Ml;
using Xunit;

namespace TgInfoBot.Tests;

/// <summary>Trains the classifier once and shares it across the test class (training is the slow part).</summary>
public sealed class TrainedMercuryClassifierFixture
{
    public MercuryClassifier Classifier { get; }

    public TrainedMercuryClassifierFixture()
    {
        Classifier = new MercuryClassifier();
        Classifier.Train(MercuryDataset.Seed);
    }
}

public class MercuryClassifierTests : IClassFixture<TrainedMercuryClassifierFixture>
{
    private readonly MercuryClassifier _sut;

    public MercuryClassifierTests(TrainedMercuryClassifierFixture fixture) => _sut = fixture.Classifier;

    [Fact]
    public void Predict_BeforeTraining_Throws()
    {
        var untrained = new MercuryClassifier();
        Assert.Throws<InvalidOperationException>(() => untrained.Predict("меркурий"));
    }

    [Fact]
    public void Train_EmptySet_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MercuryClassifier().Train([]));
    }

    [Theory]
    [InlineData("меркурий сегодня ретроградный или нет?")]
    [InlineData("опять из-за меркурия всё виснет")]
    [InlineData("когда уже закончится ретроградный меркурий")]
    public void IsMercury_ExplicitMention_ReturnsTrue(string message)
    {
        Assert.True(_sut.IsMercury(message));
    }

    [Theory]
    // Held-out, semantically Mercury-retrograde but WITHOUT the literal word.
    [InlineData("снова планета пятится, вот письма и теряются")]
    [InlineData("не подписывай договоры сейчас, ретроградный период же")]
    public void IsMercury_SemanticMentionWithoutWord_ReturnsTrue(string message)
    {
        Assert.True(_sut.IsMercury(message));
    }

    [Theory]
    [InlineData("во сколько сегодня встречаемся на обед?")]
    [InlineData("купил новый ноутбук, очень доволен")]
    [InlineData("напомни позвонить в банк завтра утром")]
    public void IsMercury_UnrelatedMessage_ReturnsFalse(string message)
    {
        Assert.False(_sut.IsMercury(message));
    }

    [Fact]
    public void Predict_ReturnsCalibratedProbability()
    {
        var p = _sut.Predict("меркурий ретроградный");
        Assert.InRange(p.Probability, 0f, 1f);
    }

    [Fact]
    public void SaveLoad_RoundTrips_PreservesPrediction()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mercury-model-{Guid.NewGuid():N}.txt");
        try
        {
            _sut.Save(path);

            var reloaded = new MercuryClassifier();
            reloaded.Load(path);

            const string msg = "опять этот меркурий всё ломает";
            Assert.Equal(_sut.IsMercury(msg), reloaded.IsMercury(msg));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

public class MercuryDatasetTests
{
    [Fact]
    public void LoadTsv_ParsesLabelsAndSkipsCommentsAndBlanks()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ds-{Guid.NewGuid():N}.tsv");
        File.WriteAllText(path,
            "# comment\n" +
            "\n" +
            "1\tмеркурий ретроградит\n" +
            "true\tопять планета пятится\n" +
            "0\tво сколько обед\n" +
            "no\tкупил ноутбук\n");
        try
        {
            var samples = MercuryDataset.LoadTsv(path);
            Assert.Equal(4, samples.Count);
            Assert.Equal(2, samples.Count(s => s.Label));
            Assert.Equal(2, samples.Count(s => !s.Label));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadTsv_InvalidLabel_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ds-{Guid.NewGuid():N}.tsv");
        File.WriteAllText(path, "maybe\tсомнительно\n");
        try
        {
            Assert.Throws<FormatException>(() => MercuryDataset.LoadTsv(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class ClassifierMatcherTests
{
    private static IConfiguration Config(params (string key, string value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.key, p.value)))
            .Build();

    private static IReadOnlyDictionary<string, InfoByDate> Commands() =>
        new Dictionary<string, InfoByDate>(StringComparer.OrdinalIgnoreCase)
        {
            ["rm"] = new InfoByDate("[rm#меркури:Меркурий:ретроградного Меркурия:Bad:0],2024-01-01:2024-01-31;"),
        };

    [Fact]
    public void Disabled_ByDefault_MatchReturnsNull()
    {
        var sut = new ClassifierMatcher(Config(), Commands(), NullLogger<ClassifierMatcher>.Instance);
        Assert.False(sut.Enabled);
        Assert.Null(sut.Match("опять этот меркурий всё ломает"));
    }

    [Fact]
    public void Enabled_WithoutValidFallback_StaysInactive()
    {
        var sut = new ClassifierMatcher(
            Config(("Classifier:Enabled", "true"), ("Classifier:FallbackCommand", "does-not-exist")),
            Commands(),
            NullLogger<ClassifierMatcher>.Instance);
        Assert.False(sut.Enabled);
    }

    [Fact]
    public void Enabled_MercuryMessage_ReturnsFallbackCommand()
    {
        var commands = Commands();
        var sut = new ClassifierMatcher(
            Config(("Classifier:Enabled", "true"), ("Classifier:FallbackCommand", "rm")),
            commands,
            NullLogger<ClassifierMatcher>.Instance);

        Assert.True(sut.Enabled);
        Assert.Same(commands["rm"], sut.Match("снова планета пятится, письма теряются"));
    }

    [Fact]
    public void Enabled_UnrelatedMessage_ReturnsNull()
    {
        var sut = new ClassifierMatcher(
            Config(("Classifier:Enabled", "true"), ("Classifier:FallbackCommand", "rm")),
            Commands(),
            NullLogger<ClassifierMatcher>.Instance);

        Assert.Null(sut.Match("во сколько сегодня обед?"));
    }
}
