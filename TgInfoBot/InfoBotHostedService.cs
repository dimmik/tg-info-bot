namespace TgInfoBot
{
    public sealed class InfoBotHostedService : BackgroundService
    {
        private readonly InfoBot _bot;

        public InfoBotHostedService(InfoBot bot)
        {
            _bot = bot;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(_bot.Stop);
            return _bot.Start();
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _bot.Stop();
            return base.StopAsync(cancellationToken);
        }
    }
}
