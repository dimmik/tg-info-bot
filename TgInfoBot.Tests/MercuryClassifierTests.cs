using TgInfoBot.Ml;
using Xunit;

namespace TgInfoBot.Tests;

/// <summary>Trains the classifier once and shares it across the test class (training is the slow part).</summary>
public sealed class TrainedMercuryClassifierFixture
{
    public MercuryClassifier Classifier { get; }

    public TrainedMercuryClassifierFixture()
    {
        Classifier = new MercuryClassifier(seed: 1);
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
        var path = Path.Combine(Path.GetTempPath(), $"mercury-model-{Guid.NewGuid():N}.zip");
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
