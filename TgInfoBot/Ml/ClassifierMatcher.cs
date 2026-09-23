namespace TgInfoBot.Ml
{
    /// <summary>
    /// Optional semantic fallback for the keyword matcher. When enabled via config, it
    /// runs <see cref="MercuryClassifier"/> on messages that no keyword matched and, if the
    /// message is classified as Mercury-related, responds with a configured fallback
    /// command's info. Disabled by default, so the bot's behaviour is unchanged unless
    /// explicitly turned on.
    ///
    /// Config (section "Classifier"):
    ///   Enabled          - bool, default false
    ///   Threshold        - float in [0,1], default 0.5
    ///   FallbackCommand  - name of a configured Command_* whose info to send on a hit
    ///   ModelPath        - optional path to a saved model file; takes priority when present
    ///   DatasetPath      - optional path to a TSV training set; used when no ModelPath
    ///                      (falls back to the built-in seed dataset if neither is set)
    /// </summary>
    public sealed class ClassifierMatcher
    {
        private readonly MercuryClassifier? _classifier;
        private readonly InfoByDate? _fallback;
        private readonly float _threshold;
        private readonly ILogger<ClassifierMatcher> _logger;

        public bool Enabled => _classifier is not null && _fallback is not null;

        public ClassifierMatcher(
            IConfiguration configuration,
            IReadOnlyDictionary<string, InfoByDate> commands,
            ILogger<ClassifierMatcher> logger)
        {
            _logger = logger;

            if (!configuration.GetValue("Classifier:Enabled", false))
            {
                _logger.LogInformation("Classifier disabled (Classifier:Enabled=false).");
                return;
            }

            _threshold = configuration.GetValue("Classifier:Threshold", 0.5f);

            var fallbackName = configuration.GetValue<string>("Classifier:FallbackCommand");
            if (string.IsNullOrWhiteSpace(fallbackName) || !commands.TryGetValue(fallbackName, out var fallback))
            {
                _logger.LogWarning(
                    "Classifier enabled but Classifier:FallbackCommand '{Fallback}' is missing or not a configured command; classifier will stay inactive.",
                    fallbackName);
                return;
            }

            var classifier = new MercuryClassifier();
            var modelPath = configuration.GetValue<string>("Classifier:ModelPath");
            try
            {
                var datasetPath = configuration.GetValue<string>("Classifier:DatasetPath");
                if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
                {
                    classifier.Load(modelPath);
                    _logger.LogInformation("Classifier model loaded from {ModelPath}.", modelPath);
                }
                else if (!string.IsNullOrWhiteSpace(datasetPath) && File.Exists(datasetPath))
                {
                    var dataset = MercuryDataset.LoadTsv(datasetPath);
                    classifier.Train(dataset);
                    _logger.LogInformation("Classifier trained from dataset {DatasetPath} ({Count} samples).", datasetPath, dataset.Count);
                }
                else
                {
                    classifier.Train(MercuryDataset.Seed);
                    _logger.LogInformation("Classifier trained from seed dataset ({Count} samples).", MercuryDataset.Seed.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize classifier; it will stay inactive.");
                return;
            }

            _classifier = classifier;
            _fallback = fallback;
            _logger.LogInformation("Classifier active: threshold={Threshold}, fallback command='{Fallback}'.", _threshold, fallbackName);
        }

        /// <summary>
        /// Returns the fallback <see cref="InfoByDate"/> when the message is classified as
        /// Mercury-related above the threshold; otherwise null (including when disabled).
        /// </summary>
        public InfoByDate? Match(string text)
        {
            if (_classifier is null || _fallback is null || string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var prediction = _classifier.Predict(text);
            if (prediction.Probability < _threshold)
            {
                return null;
            }

            _logger.LogInformation("Classifier matched message as Mercury-related (p={Probability:0.00}).", prediction.Probability);
            return _fallback;
        }
    }
}
