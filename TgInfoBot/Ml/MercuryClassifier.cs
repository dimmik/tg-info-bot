using System.Globalization;
using System.Text;

namespace TgInfoBot.Ml
{
    /// <summary>
    /// Lightweight in-process text classifier: "is this message about (retrograde)
    /// Mercury?". Plain C# bag-of-features + logistic regression: no ML framework, no
    /// reflection, so it works under Native AOT and adds next to nothing to the image or
    /// memory footprint. It complements, not replaces, the keyword matcher: the matcher
    /// catches literal/obfuscated spellings cheaply, while this catches messages that are
    /// *about* Mercury without containing the word.
    ///
    /// Features (binary, L2-normalized per message): word unigrams, word bigrams and
    /// character trigrams inside words. Char trigrams make it tolerant to Russian word
    /// endings ("меркурий" / "меркурия" / "меркурием" share most trigrams).
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
            public bool IsMercury { get; init; }

            // Probability of the positive class in [0, 1].
            public float Probability { get; init; }

            // Raw score (log-odds); useful for debugging/threshold tuning.
            public float Score { get; init; }
        }

        // Latin/Greek look-alikes -> Cyrillic, plus ё->е. Applied per character while
        // keeping word boundaries, so the text can still be split into words.
        private static readonly IReadOnlyDictionary<char, char> Homoglyphs = new Dictionary<char, char>
        {
            ['a'] = 'а', ['b'] = 'в', ['c'] = 'с', ['e'] = 'е', ['h'] = 'н',
            ['k'] = 'к', ['m'] = 'м', ['o'] = 'о', ['p'] = 'р', ['t'] = 'т',
            ['x'] = 'х', ['y'] = 'у',
            ['ё'] = 'е',
        };

        private const string ModelHeader = "# tginfo-mercury-classifier v1";

        // Training hyperparameters. Full-batch gradient descent is deterministic and fast
        // enough for datasets of up to a few thousand short messages.
        private const int Epochs = 300;
        private const double LearningRate = 1.0;
        private const double L2 = 1e-3;

        private Dictionary<string, double>? _weights;
        private double _bias;

        public bool IsTrained => _weights is not null;

        public void Train(IEnumerable<Sample> samples)
        {
            ArgumentNullException.ThrowIfNull(samples);

            var rows = samples
                .Select(s => (label: s.Label ? 1.0 : 0.0, features: Featurize(s.Text)))
                .ToList();

            if (rows.Count == 0)
            {
                throw new ArgumentException("Training set is empty.", nameof(samples));
            }

            // Class weights keep an unbalanced dataset from biasing the model toward the majority class.
            var positives = rows.Count(r => r.label > 0.5);
            var negatives = rows.Count - positives;
            var posWeight = positives > 0 ? rows.Count / (2.0 * positives) : 1.0;
            var negWeight = negatives > 0 ? rows.Count / (2.0 * negatives) : 1.0;

            var weights = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var (_, features) in rows)
            {
                foreach (var f in features.Keys)
                {
                    weights.TryAdd(f, 0.0);
                }
            }

            double bias = 0.0;
            var gradient = new Dictionary<string, double>(weights.Count, StringComparer.Ordinal);
            for (var epoch = 0; epoch < Epochs; epoch++)
            {
                gradient.Clear();
                double biasGradient = 0.0;

                foreach (var (label, features) in rows)
                {
                    var error = (Sigmoid(Dot(weights, bias, features)) - label) * (label > 0.5 ? posWeight : negWeight);
                    biasGradient += error;
                    foreach (var (f, x) in features)
                    {
                        gradient[f] = gradient.GetValueOrDefault(f) + error * x;
                    }
                }

                var n = rows.Count;
                foreach (var f in weights.Keys)
                {
                    weights[f] -= LearningRate * (gradient.GetValueOrDefault(f) / n + L2 * weights[f]);
                }

                bias -= LearningRate * biasGradient / n;
            }

            _weights = weights;
            _bias = bias;
        }

        public Prediction Predict(string text)
        {
            if (_weights is null)
            {
                throw new InvalidOperationException("Classifier is not trained. Call Train() or Load() first.");
            }

            var score = Dot(_weights, _bias, Featurize(text ?? string.Empty));
            var probability = (float)Sigmoid(score);
            return new Prediction { IsMercury = probability >= 0.5f, Probability = probability, Score = (float)score };
        }

        /// <summary>True when the message is classified as Mercury-related above the threshold.</summary>
        public bool IsMercury(string text, float threshold = 0.5f) => Predict(text).Probability >= threshold;

        /// <summary>Saves the model as a small text file: header, bias, then "feature&lt;TAB&gt;weight" lines.</summary>
        public void Save(string path)
        {
            if (_weights is null)
            {
                throw new InvalidOperationException("Nothing to save: train the classifier first.");
            }

            using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
            writer.WriteLine(ModelHeader);
            writer.WriteLine(_bias.ToString("R", CultureInfo.InvariantCulture));
            foreach (var (feature, weight) in _weights.Where(kv => kv.Value != 0.0).OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                writer.Write(feature);
                writer.Write('\t');
                writer.WriteLine(weight.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        public void Load(string path)
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
            if (reader.ReadLine() != ModelHeader)
            {
                throw new FormatException($"{path}: not a Mercury classifier model file.");
            }

            var bias = double.Parse(reader.ReadLine() ?? throw new FormatException($"{path}: missing bias."), CultureInfo.InvariantCulture);
            var weights = new Dictionary<string, double>(StringComparer.Ordinal);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var tab = line.LastIndexOf('\t');
                if (tab <= 0)
                {
                    throw new FormatException($"{path}: malformed line '{line}'.");
                }

                weights[line[..tab]] = double.Parse(line[(tab + 1)..], CultureInfo.InvariantCulture);
            }

            _weights = weights;
            _bias = bias;
        }

        /// <summary>
        /// Lower-cases and canonicalizes look-alike letters (Latin->Cyrillic, ё->е) while
        /// preserving whitespace so word-level features still work. Note: spaced-out
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

        internal static Dictionary<string, double> Featurize(string text)
        {
            var words = Tokenize(Normalize(text));
            var features = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < words.Count; i++)
            {
                features.Add("w:" + words[i]);
                if (i > 0)
                {
                    features.Add("b:" + words[i - 1] + " " + words[i]);
                }

                var padded = "_" + words[i] + "_";
                for (var j = 0; j + 3 <= padded.Length; j++)
                {
                    features.Add("c:" + padded.Substring(j, 3));
                }
            }

            // Binary features, L2-normalized so long messages don't dominate by sheer length.
            var value = features.Count > 0 ? 1.0 / Math.Sqrt(features.Count) : 0.0;
            return features.ToDictionary(f => f, _ => value, StringComparer.Ordinal);
        }

        private static List<string> Tokenize(string normalized)
        {
            var words = new List<string>();
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(ch);
                }
                else if (sb.Length > 0)
                {
                    words.Add(sb.ToString());
                    sb.Clear();
                }
            }

            if (sb.Length > 0)
            {
                words.Add(sb.ToString());
            }

            return words;
        }

        private static double Dot(Dictionary<string, double> weights, double bias, Dictionary<string, double> features)
        {
            var sum = bias;
            foreach (var (f, x) in features)
            {
                if (weights.TryGetValue(f, out var w))
                {
                    sum += w * x;
                }
            }

            return sum;
        }

        private static double Sigmoid(double z) => 1.0 / (1.0 + Math.Exp(-z));
    }
}
