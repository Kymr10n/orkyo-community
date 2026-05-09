# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/migrations/Orkyo.Community.Migrations.csproj backend/migrations/
COPY backend/src/Orkyo.Community.csproj backend/src/
COPY backend/worker/Orkyo.Community.Worker.csproj backend/worker/
COPY nuget.config ./

RUN --mount=type=secret,id=nuget_token,required=true \
    dotnet nuget add source https://nuget.pkg.github.com/Kymr10n/index.json \
      --name github --username Kymr10n \
      --password "$(cat /run/secrets/nuget_token)" \
      --store-password-in-clear-text \
    && dotnet restore backend/worker/Orkyo.Community.Worker.csproj -r linux-musl-x64

COPY backend/ backend/

RUN dotnet publish backend/worker/Orkyo.Community.Worker.csproj \
    -c Release -o /app/publish --no-restore \
    -r linux-musl-x64 --no-self-contained /p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
LABEL org.opencontainers.image.source=https://github.com/Kymr10n/orkyo-community
WORKDIR /app
COPY --from=build /app/publish .

RUN adduser -D -u 999 -s /sbin/nologin appuser \
    && chown -R appuser:appuser /app
USER appuser

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD pgrep -f Orkyo.Community.Worker.dll || exit 1

ENTRYPOINT ["dotnet", "Orkyo.Community.Worker.dll"]
