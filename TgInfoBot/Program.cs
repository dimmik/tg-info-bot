using System.Collections.ObjectModel;
using TgInfoBot;

var builder = Host.CreateApplicationBuilder(args);

var conf = builder.Configuration;
var tgToken = conf.GetValue<string>("TgToken") ?? string.Empty;
if (string.IsNullOrWhiteSpace(tgToken) || string.Equals(tgToken, "wrong", StringComparison.OrdinalIgnoreCase) || string.Equals(tgToken, "xxx", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Telegram bot token is not configured. Set TgToken via user secrets or environment variables.");
}

var commandEntries = conf.AsEnumerable()
    .Where(kv =>
    {
        if (!kv.Key.StartsWith("Command_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(kv.Value))
        {
            return false;
        }

        var suffix = kv.Key.Length > "Command_".Length
            ? kv.Key.Substring("Command_".Length)
            : string.Empty;

        if (suffix.Contains("_", StringComparison.Ordinal))
        {
            return false;
        }

        return kv.Value.TrimStart().StartsWith("[", StringComparison.Ordinal);
    })
    .ToArray();

if (commandEntries.Length == 0)
{
    throw new InvalidOperationException("No bot commands configured. Add at least one Command_* setting.");
}

// Commands that should use live JPL Horizons data.
// Config key: LiveDataCommands (comma-separated, e.g. "RM").
var liveCommands = (conf.GetValue<string>("LiveDataCommands") ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

var commands = new Dictionary<string, InfoByDate>(StringComparer.OrdinalIgnoreCase);
foreach (var entry in commandEntries)
{
    var commandName = entry.Key["Command_".Length..].Trim();
    if (string.IsNullOrWhiteSpace(commandName))
    {
        throw new InvalidOperationException($"Invalid command key '{entry.Key}'.");
    }

    try
    {
        commands[commandName] = new InfoByDate(entry.Value!, requireDates: !liveCommands.Contains(commandName));
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Invalid command configuration for key '{entry.Key}'.", ex);
    }
}

// Configure JPL Horizons API options
var jplOptions = new JplHorizonsOptions();
conf.GetSection("JplHorizons").Bind(jplOptions);
builder.Services.AddSingleton(jplOptions);

// Configure Mercury retrograde refresh options
var refreshOptions = new MercuryRetrogradeRefreshOptions();
conf.GetSection("MercuryRetrogradeRefresh").Bind(refreshOptions);
builder.Services.AddSingleton(refreshOptions);

builder.Services.AddHttpClient<JplHorizonsClient>();
builder.Services.AddSingleton<MercuryRetrogradeProvider>();
builder.Services.AddHostedService<MercuryRetrogradeDateRefreshService>();

builder.Services.AddSingleton<IReadOnlyDictionary<string, InfoByDate>>(sp =>
{
    // Wire up live provider to commands that opted in.
    if (liveCommands.Count > 0)
    {
        var provider = sp.GetRequiredService<MercuryRetrogradeProvider>();
        foreach (var cmd in liveCommands)
        {
            if (commands.TryGetValue(cmd, out var infoByDate))
            {
                infoByDate.LiveProvider = provider;
            }
        }
    }

    return new ReadOnlyDictionary<string, InfoByDate>(commands);
});
builder.Services.AddSingleton<TgInfoBot.Ml.ClassifierMatcher>();
builder.Services.AddSingleton<InfoBot>();
builder.Services.AddHostedService<InfoBotHostedService>();

builder.Build().Run();
