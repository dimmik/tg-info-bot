using Microsoft.ML;
using Microsoft.ML.Data;
using System.Text;

namespace TgInfoBot.Ml
{
    /// <summary>
    /// Lightweight in-process text classifier: "is this message about (retrograde)
    /// Mercury?". Built on ML.NET (bag-of-words + logistic regression), so it runs on
    /// CPU with no external services and a tiny model — suitable even for a single-board
    /// computer. It complements, not replaces, the keyword matcher: the matcher catches
    /// literal/obfuscated spellings cheaply, while this catches messages that are *about*
    /// Mercury without containing the word.
    /// </summary>
    public sealed class MercuryClassifier
    {
        public sealed class Sample
        {
            public bool Label { get; set; }
            public string Text { get; set; } = string.Empty;
        }

        public sealed class Prediction
        {
            [ColumnName("PredictedLabel")]
            public bool IsMercury { get; set; }

            // Calibrated probability of the positive class in [0, 1].
            public float Probability { get; set; }

            // Raw score (log-odds); useful for debugging/threshold tuning.
            public float Score { get; set; }
        }

        // Latin/Greek look-alikes -> Cyrillic, plus ё->е. Applied per character while
        // keeping word boundaries, so the featurizer can still tokenize into words.
        private static readonly IReadOnlyDictionary<char, char> Homoglyphs = new Dictionary<char, char>
        {
            ['a'] = 'а', ['b'] = 'в', ['c'] = 'с', ['e'] = 'е', ['h'] = 'н',
            ['k'] = 'к', ['m'] = 'м', ['o'] = 'о', ['p'] = 'р', ['t'] = 'т',
            ['x'] = 'х', ['y'] = 'у',
            ['ё'] = 'е',
        };

        private readonly MLContext _ml;
        private ITransformer? _model;
        private DataViewSchema? _inputSchema;
        private PredictionEngine<Sample, Prediction>? _engine;

        /// <param name="seed">Fixed seed keeps training deterministic (handy for tests).</param>
        public MercuryClassifier(int? seed = 1)
        {
            _ml = new MLContext(seed);
        }

        public bool IsTrained => _engine is not null;

        public void Train(IEnumerable<Sample> samples)
        {
            ArgumentNullException.ThrowIfNull(samples);

            var normalized = samples
                .Select(s => new Sample { Label = s.Label, Text = Normalize(s.Text) })
                .ToList();

            if (normalized.Count == 0)
            {
                throw new ArgumentException("Training set is empty.", nameof(samples));
            }

            var data = _ml.Data.LoadFromEnumerable(normalized);
            _inputSchema = data.Schema;

            var pipeline = _ml.Transforms.Text
                .FeaturizeText("Features", nameof(Sample.Text))
                .Append(_ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                    labelColumnName: nameof(Sample.Label),
                    featureColumnName: "Features"));

            _model = pipeline.Fit(data);
            _engine = _ml.Model.CreatePredictionEngine<Sample, Prediction>(_model);
        }

        public Prediction Predict(string text)
        {
            if (_engine is null)
            {
                throw new InvalidOperationException("Classifier is not trained. Call Train() or Load() first.");
            }

            return _engine.Predict(new Sample { Text = Normalize(text ?? string.Empty) });
        }

        /// <summary>True when the message is classified as Mercury-related above the threshold.</summary>
        public bool IsMercury(string text, float threshold = 0.5f) => Predict(text).Probability >= threshold;

        public void Save(string path)
        {
            if (_model is null || _inputSchema is null)
            {
                throw new InvalidOperationException("Nothing to save: train the classifier first.");
            }

            _ml.Model.Save(_model, _inputSchema, path);
        }

        public void Load(string path)
        {
            _model = _ml.Model.Load(path, out _inputSchema);
            _engine = _ml.Model.CreatePredictionEngine<Sample, Prediction>(_model);
        }

        /// <summary>
        /// Lower-cases and canonicalizes look-alike letters (Latin->Cyrillic, ё->е) while
        /// preserving whitespace so word-level featurization still works. Note: spaced-out
        /// obfuscation ("м е р к у р и и") is intentionally NOT collapsed here — that is the
        /// keyword matcher's job; a classifier needs intact word boundaries.
        /// </summary>
        internal static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                var lower = char.ToLowerInvariant(ch);
                sb.Append(Homoglyphs.TryGetValue(lower, out var mapped) ? mapped : lower);
            }

            return sb.ToString();
        }
    }
}
