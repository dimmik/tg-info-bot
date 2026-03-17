namespace TgInfoBot
{
    /// <summary>
    /// Computes Mercury retrograde date ranges from ecliptic longitude time series
    /// and exposes them as a live-updated list.
    /// </summary>
    public sealed class MercuryRetrogradeProvider : IMercuryRetrogradeProvider
    {
        private volatile (DateTimeOffset from, DateTimeOffset to)[] _ranges = [];
        private DateTimeOffset _lastUpdated = DateTimeOffset.MinValue;

        private readonly JplHorizonsClient _jpl;
        private readonly ILogger<MercuryRetrogradeProvider> _logger;

        // How many years ahead/behind current year to fetch.
        private const int FetchWindowYears = 2;

        public MercuryRetrogradeProvider(JplHorizonsClient jpl, ILogger<MercuryRetrogradeProvider> logger)
        {
            _jpl = jpl;
            _logger = logger;
        }

        /// <summary>Current retrograde ranges. Empty until first refresh completes.</summary>
        public (DateTimeOffset from, DateTimeOffset to)[] Ranges => _ranges;

        public DateTimeOffset LastUpdated => _lastUpdated;

        public bool HasData => _ranges.Length > 0;

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            int currentYear = DateTimeOffset.UtcNow.Year;
            int from = currentYear - 1;
            int to   = currentYear + FetchWindowYears;

            _logger.LogInformation("Fetching Mercury ephemeris from JPL Horizons ({From}-{To})...", from, to);

            var longitudes = await _jpl.FetchLongitudesAsync(from, to, ct);
            if (longitudes.Count == 0)
            {
                _logger.LogWarning("JPL returned empty longitude data.");
                return;
            }

            var ranges = ComputeRetrogradeRanges(longitudes);
            _ranges = ranges;
            _lastUpdated = DateTimeOffset.UtcNow;

            _logger.LogInformation("Loaded {Count} Mercury retrograde periods from JPL.", ranges.Length);
            foreach (var r in ranges)
            {
                _logger.LogInformation("  Retrograde: {From:yyyy-MM-dd} – {To:yyyy-MM-dd}", r.from, r.to);
            }
        }

        internal static (DateTimeOffset from, DateTimeOffset to)[] ComputeRetrogradeRanges(
            List<(DateOnly date, double longitude)> rows)
        {
            var ranges = new List<(DateTimeOffset, DateTimeOffset)>();
            bool inRetro = false;
            DateOnly retroStart = default;

            for (int i = 1; i < rows.Count; i++)
            {
                double delta = rows[i].longitude - rows[i - 1].longitude;

                // Normalise wrap-around (e.g. 359 -> 1 should not register as -358)
                if (delta > 180)  delta -= 360;
                if (delta < -180) delta += 360;

                if (delta < 0 && !inRetro)
                {
                    inRetro = true;
                    retroStart = rows[i - 1].date;
                }
                else if (delta >= 0 && inRetro)
                {
                    inRetro = false;
                    var endDate = rows[i].date;
                    ranges.Add((
                        new DateTimeOffset(retroStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                        // Include the full end day (23:59:59)
                        new DateTimeOffset(endDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)
                    ));
                }
            }

            // If still in retrograde at end of data, leave it open-ended (truncate at last data point)
            if (inRetro)
            {
                var endDate = rows[^1].date;
                ranges.Add((
                    new DateTimeOffset(retroStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                    new DateTimeOffset(endDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero)
                ));
            }

            return ranges.ToArray();
        }
    }
}
