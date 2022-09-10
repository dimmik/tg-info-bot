using Microsoft.VisualBasic;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TgInfoBot
{
    public class InfoBot : IDisposable
    {
        private readonly ITelegramBotClient botClient;
        private readonly Dictionary<string, InfoByDate> Commands;
        public InfoBot(string token, Dictionary<string, InfoByDate> commands)
        {
            botClient = new TelegramBotClient(token);
            Commands = commands;
        }

        private readonly CancellationTokenSource cts = new();
        public async Task Start()
        {

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message }  // messages only
            };
            await botClient.ReceiveAsync(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );
        }

        public void Stop()
        {
            cts.Cancel();
        }

        private async Task HandlePollingErrorAsync(ITelegramBotClient client, Exception ex, CancellationToken t)
        {
            //throw new NotImplementedException();
        }

        private async Task HandleUpdateAsync(ITelegramBotClient client, Update upd, CancellationToken t)
        {
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (upd.Message is not { } message)
                return;
            // Only process text messages
            if (message.Text is not { } messageText)
                return;
            if (messageText == null)
                return;
            if (messageText.StartsWith("/"))
            {
                var command = messageText.Substring(1).Split(" ")[0].Trim();
                if (Commands.ContainsKey(command)) {
                    var infoer = Commands[command];
                    var ret = infoer.GetInfoNow();
                    var chatId = upd.Message.Chat.Id;
                    _ = await client.SendTextMessageAsync(
                        chatId: chatId,
                        text: ret,
                        cancellationToken: t);
                }
            }
        }

        public void Dispose()
        {
            Stop();
            ((IDisposable)cts).Dispose();
        }
    }
}
