namespace TgInfoBot
{
    /// <summary>
    /// Configuration for JPL Horizons API client.
    /// </summary>
    public class JplHorizonsOptions
    {
        /// <summary>
        /// Base URL for JPL Horizons API.
        /// Default: "https://ssd.jpl.nasa.gov/api/horizons.api"
        /// </summary>
        public string BaseUrl { get; set; } = "https://ssd.jpl.nasa.gov/api/horizons.api";

        /// <summary>
        /// NAIF body ID for Mercury.
        /// Default: "199"
        /// </summary>
        public string MercuryId { get; set; } = "199";

        /// <summary>
        /// Step size for ephemeris data (e.g., "1d" for one day).
        /// Default: "1d"
        /// </summary>
        public string StepSize { get; set; } = "1d";

        /// <summary>
        /// JPL Horizons QUANTITIES parameter (33-digit code for requested quantities).
        /// "31" = Ecliptic longitude and latitude.
        /// Default: "31"
        /// </summary>
        public string Quantities { get; set; } = "31";

        /// <summary>
        /// Whether to request CSV format (yes) or alternative format.
        /// Default: "YES"
        /// </summary>
        public string CsvFormat { get; set; } = "YES";

        /// <summary>
        /// Whether to include object data in response.
        /// Default: "NO"
        /// </summary>
        public string ObjData { get; set; } = "NO";
    }
}
