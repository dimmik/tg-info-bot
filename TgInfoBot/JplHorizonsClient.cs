using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TgInfoBot
{
    /// <summary>
    /// Retrieves raw ecliptic longitude data for Mercury from the NASA/JPL Horizons API.
    /// </summary>
    public sealed class JplHorizonsClient
    {
        private readonly HttpClient _http;
        private const string BaseUrl = "https://ssd.jpl.nasa.gov/api/horizons.api";

        // Mercury major body ID.
        private const string MercuryId = "199";

        public JplHorizonsClient(HttpClient http)
        {
            _http = http;
        }

        /// <summary>
        /// Fetches daily ecliptic longitudes for Mercury in <paramref name="year"/> ± 1 year.
        /// Returns a list of (date, longitude) tuples ordered by date.
        /// </summary>
        public async Task<List<(DateOnly date, double longitude)>> FetchLongitudesAsync(
            int yearFrom, int yearTo, CancellationToken ct = default)
        {
            var startTime = new DateOnly(yearFrom, 1, 1).ToString("yyyy-MM-dd");
            var stopTime  = new DateOnly(yearTo, 12, 31).ToString("yyyy-MM-dd");

            var url = $"{BaseUrl}?format=json" +
                $"&COMMAND='{MercuryId}'" +
                $"&EPHEM_TYPE=OBSERVER" +
                $"&CENTER='500@399'" +     // geocentric
                $"&START_TIME='{startTime}'" +
                $"&STOP_TIME='{stopTime}'" +
                $"&STEP_SIZE='1d'" +
                $"&QUANTITIES='31'" +      // ecliptic longitude
                $"&CSV_FORMAT=YES" +
                $"&OBJ_DATA=NO";

            using var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                throw new InvalidOperationException($"JPL Horizons API error: {err.GetString()}");
            }

            if (!doc.RootElement.TryGetProperty("result", out var resultEl))
            {
                throw new InvalidOperationException("JPL Horizons API returned no 'result' field.");
            }

            var result = resultEl.GetString() ?? string.Empty;
            return ParseLongitudes(result);
        }

        internal static List<(DateOnly date, double longitude)> ParseLongitudes(string text)
        {
            var rows = new List<(DateOnly, double)>();
            bool inData = false;

            foreach (var line in text.Split('\n'))
            {
                if (line.Contains("$$SOE")) { inData = true; continue; }
                if (line.Contains("$$EOE")) { break; }
                if (!inData || string.IsNullOrWhiteSpace(line)) continue;

                // CSV columns: "YYYY-Mon-DD HH:MM, , , longitude, latitude,"
                var parts = line.Split(',');
                if (parts.Length < 4) continue;

                var datePart = parts[0].Trim();
                if (!TryParseDate(datePart, out var date)) continue;
                if (!double.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) continue;

                rows.Add((date, lon));
            }

            return rows;
        }

        private static bool TryParseDate(string s, out DateOnly date)
        {
            // "2026-Jan-01 00:00" or "2026-Feb-26 00:00"
            if (s.Length >= 11 &&
                DateOnly.TryParseExact(s[..11], "yyyy-MMM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }

            date = default;
            return false;
        }
    }
}
