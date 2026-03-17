namespace TgInfoBot
{
    /// <summary>
    /// Background service that refreshes Mercury retrograde data from JPL once per day.
    /// On startup it immediately does the first fetch; on failure it retries with back-off.
    /// </summary>
    public sealed class MercuryRetrogradeDateRefreshService : BackgroundService
    {
        private readonly MercuryRetrogradeProvider _provider;
        private readonly ILogger<MercuryRetrogradeDateRefreshService> _logger;

        private static readonly TimeSpan RefreshInterval  = TimeSpan.FromHours(24);
        private static readonly TimeSpan RetryInterval    = TimeSpan.FromMinutes(30);

        public MercuryRetrogradeDateRefreshService(
            MercuryRetrogradeProvider provider,
            ILogger<MercuryRetrogradeDateRefreshService> logger)
        {
            _provider = provider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _provider.RefreshAsync(stoppingToken);
                    await Task.Delay(RefreshInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh Mercury retrograde data from JPL. Retrying in {Retry} min.", RetryInterval.TotalMinutes);
                    await Task.Delay(RetryInterval, stoppingToken);
                }
            }
        }
    }
}
