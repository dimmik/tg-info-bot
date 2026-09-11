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
        private readonly MercuryRetrogradeRefreshOptions _options;

        public MercuryRetrogradeDateRefreshService(
            MercuryRetrogradeProvider provider,
            ILogger<MercuryRetrogradeDateRefreshService> logger,
            MercuryRetrogradeRefreshOptions options)
        {
            _provider = provider;
            _logger = logger;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _provider.RefreshAsync(stoppingToken);
                    await Task.Delay(_options.RefreshInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh Mercury retrograde data from JPL. Retrying in {Retry} min.", _options.RetryInterval.TotalMinutes);
                    await Task.Delay(_options.RetryInterval, stoppingToken);
                }
            }
        }
    }
}
