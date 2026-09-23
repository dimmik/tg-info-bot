# Native AOT build. Runs natively on the builder and cross-compiles to the
# target arch, so no QEMU emulation is needed for arm64.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble-aot AS build
ARG BUILDARCH
ARG TARGETARCH
# The -aot SDK image has clang but no arm64 sysroot; the cross gcc packages provide it.
RUN if [ "$TARGETARCH" != "$BUILDARCH" ]; then \
        apt-get update \
        && apt-get install -y --no-install-recommends gcc-aarch64-linux-gnu libc6-dev-arm64-cross \
        && rm -rf /var/lib/apt/lists/*; \
    fi
WORKDIR /src
COPY ["TgInfoBot.csproj", "."]
RUN dotnet restore -a $TARGETARCH "./TgInfoBot.csproj"
COPY . .
RUN dotnet publish "TgInfoBot.csproj" -c Release -a $TARGETARCH --no-restore -o /app/publish \
    && rm -f /app/publish/*.dbg /app/publish/*.pdb

# runtime-deps chiseled: only libc/libssl/ca-certificates, no shell, runs as non-root.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS final
WORKDIR /app
# Lets `podman auto-update` track this container against the registry tag.
# Podman copies image labels onto the container at create time, so a container
# started from a fully-qualified ref (ghcr.io/dimmik/tg-info-bot:latest) inside a
# systemd unit becomes auto-update-eligible without extra flags.
LABEL io.containers.autoupdate=registry
COPY --from=build /app/publish .
ENTRYPOINT ["./TgInfoBot"]
