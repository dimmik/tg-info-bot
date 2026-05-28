using System.Collections;
using System.Collections.ObjectModel;
using TgInfoBot;


IDictionary variables = Environment.GetEnvironmentVariables();

foreach (DictionaryEntry entry in variables)
{
    Console.WriteLine($"{entry.Key} = {entry.Value}");
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var conf = builder.Configuration;
var tgToken = conf.GetValue<string>("TgToken") ?? string.Empty;
if (string.IsNullOrWhiteSpace(tgToken) || string.Equals(tgToken, "wrong", StringComparison.OrdinalIgnoreCase) || string.Equals(tgToken, "xxx", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Telegram bot token is not configured. Set TgToken via user secrets or environment variables.");
}

var commandEntries = conf.AsEnumerable()
    .Where(kv =>
    {
        Console.WriteLine($"config: {kv.Key} = {kv.Value}");
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

string seccode = conf.GetValue<string>("TgBotSecretCode") ?? "adk";

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
builder.Services.AddSingleton<InfoBot>();
builder.Services.AddHostedService<InfoBotHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet($"/config/{seccode}/enable", (InfoBot tgBot) =>
{
    tgBot.Enabled = !tgBot.Enabled;
    return $"tgBotEnabled: {tgBot.Enabled}";
});

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateTime.Now.AddDays(index),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

internal record WeatherForecast(DateTime Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}