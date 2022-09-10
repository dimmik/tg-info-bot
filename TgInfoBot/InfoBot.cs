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
        public bool Enabled { get; set; }
        public InfoBot(string token, Dictionary<string, InfoByDate> commands, bool enabled)
        {
            botClient = new TelegramBotClient(token);
            Commands = commands;
            Enabled = enabled;
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

        private Task HandlePollingErrorAsync(ITelegramBotClient client, Exception ex, CancellationToken t)
        {
            //throw new NotImplementedException();
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
            if (messageText == null)
                return;
            if (messageText.StartsWith("/"))
            {
                var chatId = upd.Message.Chat.Id;
                var command = messageText.Substring(1).Split(" ")[0].Trim();
                Console.WriteLine($"Command: {command}");
                if (command == "off" && Enabled)
                {
                    Console.WriteLine("Enable");
                    Enabled = false;
                    _ = await client.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Заткнулся",
                            cancellationToken: t);
                    return;
                }
                if (command == "on" && !Enabled)
                {
                    Console.WriteLine("Disable");
                    Enabled = true;
                    _ = await client.SendTextMessageAsync(
                            chatId: chatId,
                            text: "ну ок",
                            cancellationToken: t);
                    return;
                }
                if (command == "status")
                {
                    Console.WriteLine($"Report status {Enabled}");
                    _ = await client.SendTextMessageAsync(
                            chatId: chatId,
                            text: $"{(Enabled ? "дада" : "сплю")}",
                            cancellationToken: t);
                    return;
                }
                if (Commands.ContainsKey(command)) {
                    Console.WriteLine("Write an info");
                    var infoer = Commands[command];
                    var ret = infoer.GetInfoNow();
                    if (Enabled)
                    {
                        _ = await client.SendTextMessageAsync(
                            chatId: chatId,
                            text: ret,
                            cancellationToken: t);
                    }
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
