using System.Collections.Generic;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TgInfoBot
{
    public class InfoBot : IDisposable
    {
        private readonly ITelegramBotClient botClient;
        private readonly IReadOnlyDictionary<string, InfoByDate> Commands;
        private readonly ILogger<InfoBot> _logger;
        public bool Enabled { get; set; }
        public InfoBot(IConfiguration configuration, IReadOnlyDictionary<string, InfoByDate> commands, ILogger<InfoBot> logger)
        {
            var token = configuration.GetValue<string>("TgToken") ?? string.Empty;
            botClient = new TelegramBotClient(token);
            Commands = commands;
            Enabled = configuration.GetValue("TgInfoEnabled", false);
            _logger = logger;
        }

        private readonly CancellationTokenSource cts = new();
        public async Task Start()
        {
            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message }  // messages only
            };
            _logger.LogInformation("Telegram bot receiving started");
            await botClient.ReceiveAsync(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );
        }

        public void Stop()
        {
            _logger.LogInformation("Telegram bot receiving stopping");
            cts.Cancel();
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient client, Exception ex, CancellationToken t)
        {
            _logger.LogError(ex, "Telegram polling error: {Message}", ex.Message);
            return Task.CompletedTask;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient client, Update upd, CancellationToken t)
        {
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (upd.Message is not { } message)
                return;
            // Only process text messages
            if (message.Text is not { } messageText)
                return;

            if (string.IsNullOrWhiteSpace(messageText))
                return;

            var isSlashCommand = messageText.StartsWith("/", StringComparison.Ordinal);
            var command = isSlashCommand ? ParseCommandName(messageText) : null;
            var infoer = TryMatchInfoProcessor(messageText);

            if (command is null && infoer is null)
            {
                return;
            }

            var chatId = upd.Message!.Chat.Id;
            if (!string.IsNullOrEmpty(command))
            {
                _logger.LogInformation("Command: {Command}", command);
                if (command == "off" && Enabled)
                {
                    Enabled = false;
                    _ = await client.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Заткнулся",
                            cancellationToken: t);
                    return;
                }
                if (command == "on" && !Enabled)
                {
                    Enabled = true;
                    _ = await client.SendTextMessageAsync(
                            chatId: chatId,
                            text: "ну ок",
                            cancellationToken: t);
                    return;
                }
                if (command == "status")
                {
                    _ = await client.SendTextMessageAsync(
                            chatId: chatId,
                            text: $"{(Enabled ? "дада" : "сплю")}",
                            cancellationToken: t);
                    return;
                }
                if (Commands.TryGetValue(command, out var infoByCommand))
                {
                    await WriteInfo(client, chatId, infoByCommand, t);
                    return;
                }
            }

            if (infoer is not null)
            {
                await WriteInfo(client, chatId, infoer, t);
            }
        }

        private async Task WriteInfo(ITelegramBotClient client, long chatId, InfoByDate infoer, CancellationToken t)
        {
            _logger.LogInformation("Write info for chat {ChatId}", chatId);
            var ret = infoer.GetInfoNow();
            if (Enabled)
            {
                _ = await client.SendTextMessageAsync(
                    chatId: chatId,
                    text: ret,
                    cancellationToken: t);
            }
        }

        private static string? ParseCommandName(string messageText)
        {
            var payload = messageText.TrimStart('/').Trim();
            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            var command = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            return command.Length == 0 ? null : command;
        }

        private InfoByDate? TryMatchInfoProcessor(string messageText)
        {
            foreach (var candidate in Commands.Values)
            {
                if (candidate.Accept(messageText))
                {
                    return candidate;
                }
            }

            return null;
        }

        public void Dispose()
        {
            Stop();
            ((IDisposable)cts).Dispose();
        }
    }
}
