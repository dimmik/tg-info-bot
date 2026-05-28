namespace TgInfoBot
{
    /// <summary>
    /// Configuration for the Mercury retrograde data refresh service.
    /// </summary>
    public class MercuryRetrogradeRefreshOptions
    {
        /// <summary>
        /// How often to refresh Mercury retrograde data from JPL.
        /// Default: 24 hours.
        /// </summary>
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// How long to wait before retrying after a failed refresh.
        /// Default: 30 minutes.
        /// </summary>
        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromMinutes(30);
    }
}
