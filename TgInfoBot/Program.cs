using System.Linq;
using TgInfoBot;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var conf = app.Configuration;
var commands = conf.AsEnumerable().Where(kv => kv.Key.StartsWith("Command_"))
    .ToDictionary(k => k.Key.Trim()["Command_".Length..].ToLower(), v => new InfoByDate(v.Value));

var tgToken = conf.GetValue("TgToken", "wrong");
bool enabled = conf.GetValue("TgInfoEnabled", false);
string seccode = conf.GetValue("TgBotSecretCode", "adk");
using var tgBot = new InfoBot(tgToken, commands, enabled);
Task tgBotTask = tgBot.Start();


var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet($"/config/{seccode}/enable", () => 
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

await Task.Delay(-1);
//app.Run();

internal record WeatherForecast(DateTime Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}