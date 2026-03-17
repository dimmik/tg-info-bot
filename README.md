# tg-info-bot

Telegram bot that tracks Mercury retrograde periods and reports their status on demand.
The bot responds to slash commands and keyword messages, telling the user whether Mercury
is currently retrograde, how many days remain, or when the next period begins.

Date ranges are fetched automatically from the NASA/JPL Horizons API once per day
and can fall back to static dates configured locally.

## Requirements

- .NET 10 SDK
- A Telegram bot token (obtain from [@BotFather](https://t.me/botfather))

## Configuration

All settings are read from `appsettings.json`, environment variables, or .NET User Secrets.
Never commit a real token to version control.

| Key | Default | Description |
|-----|---------|-------------|
| `TgToken` | _(required)_ | Telegram bot token. Must be set via User Secrets or environment variable. |
| `TgInfoEnabled` | `false` | Whether the bot sends replies on startup. Can be toggled at runtime. |
| `TgBotSecretCode` | `adk` | Secret path segment for the HTTP enable/disable endpoint. |
| `LiveDataCommands` | `""` | Comma-separated list of command names that should use live JPL data (e.g. `RM`). Empty disables live fetching. |
| `Command_<NAME>` | _(none)_ | One or more command definitions (see format below). At least one is required. `<NAME>` should not contain `_`. |

### Setting the token with User Secrets (recommended for local development)

```
dotnet user-secrets set "TgToken" "your-token-here"
```

Or via environment variable:

```
export TgToken=your-token-here
```

### Command definition format

```text
Command_RM = [rm#keyword1#keyword2:Description:DescriptionGenitive:Good|Bad:AlertDays],YYYY-MM-DD:YYYY-MM-DD;...
```

Fields inside `[...]`, separated by `:`:
1. `commandName#keyword1#keyword2...` - slash command name and optional trigger keywords
2. `Description` - used in the "in progress" message
3. `DescriptionGenitive` - used in the "no event" / "next event" message
4. `Good` or `Bad` - controls the tone of the response (celebratory vs. alarming)
5. `AlertDays` - warn if the next period starts within this many days (e.g. `3`)

After the `]` and a comma: semicolon-separated date ranges `YYYY-MM-DD:YYYY-MM-DD`.
These are used as a fallback when `LiveDataCommands` does not include this command.

If a command is listed in `LiveDataCommands`, static ranges may be omitted (for example: `"[rm#...:Bad:3]"`).

Example:

```json
"Command_RM": "[rm#меркур#ретрогр:Ретроградный Меркурий:Ретроградного Меркурия:Bad:3],2025-03-15:2025-04-07;"
```

## Live data from NASA/JPL Horizons

When a command name appears in `LiveDataCommands`, the bot fetches daily ecliptic
longitudes for Mercury from the [JPL Horizons API](https://ssd.jpl.nasa.gov/horizons/)
and computes retrograde periods automatically (retrograde = days where the ecliptic
longitude decreases). Data covers the previous year through two years ahead.

The service refreshes every 24 hours. On failure it retries after 30 minutes.
Until the first successful fetch the bot falls back to the static dates from config.

To disable live fetching and rely solely on the config dates, set `LiveDataCommands`
to an empty string.

## Running locally

```
cd TgInfoBot
dotnet user-secrets set "TgToken" "your-token-here"
dotnet run
```

The Swagger UI is available at `http://localhost:<port>/swagger` in Development mode.

## Running with Docker

```
cd TgInfoBot
docker build -t tg-info-bot .
docker run -e TgToken=your-token-here -e TgInfoEnabled=true -e LiveDataCommands=RM tg-info-bot
```

## CI/CD

- `.github/workflows/tests.yml` runs restore/build/tests on push to `master` and on every pull request.
- `.github/workflows/docker-publish.yml` builds and publishes a multi-platform image (`linux/amd64`, `linux/arm64`) to `ghcr.io/<owner>/<repo>` on push to `master`.
- Docker tags include `latest` (default branch), `sha-*`, and branch-based tags.

## HTTP management endpoint

```
GET /config/{TgBotSecretCode}/enable
```

Toggles `TgInfoEnabled` at runtime and returns the new state. Change `TgBotSecretCode`
from its default value before deploying.

## Bot commands

| Command | Description |
|---------|-------------|
| `/rm` | Report Mercury retrograde status |
| `/status` | Show whether the bot is currently responding |
| `/on` | Enable responses |
| `/off` | Disable responses |

Any message containing a configured keyword (e.g. "меркур", "ретрогр") also triggers a response.

## Project structure

```
TgInfoBot/
  Program.cs                          - Application entry point, DI setup
  InfoBot.cs                          - Telegram update handler
  InfoBotHostedService.cs             - BackgroundService wrapper for InfoBot
  InfoByDate.cs                       - Date-range logic and response formatting
  IMessageProcessor.cs                - Accept(string) interface
  JplHorizonsClient.cs                - HTTP client for NASA/JPL Horizons API
  MercuryRetrogradeProvider.cs        - Retrograde period computation from JPL data
  MercuryRetrogradeDateRefreshService.cs - Daily background refresh of JPL data
  appsettings.json                    - Base configuration (no secrets)
  appsettings.Development.json        - Development overrides (no real token)
  Dockerfile                          - Multi-stage Docker build (.NET 10)
```