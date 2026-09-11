#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["TgInfoBot.csproj", "."]
RUN dotnet restore "./TgInfoBot.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "TgInfoBot.csproj" -c Release -o /app/build

FROM --platform=$BUILDPLATFORM build AS publish
RUN dotnet publish "TgInfoBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
# Lets `podman auto-update` track this container against the registry tag.
# Podman copies image labels onto the container at create time, so a container
# started from a fully-qualified ref (ghcr.io/dimmik/tg-info-bot:latest) inside a
# systemd unit becomes auto-update-eligible without extra flags.
LABEL io.containers.autoupdate=registry
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TgInfoBot.dll"]