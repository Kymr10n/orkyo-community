# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/migrations/Orkyo.Community.Migrations.csproj backend/migrations/
COPY backend/src/Orkyo.Community.csproj backend/src/
COPY backend/worker/Orkyo.Community.Worker.csproj backend/worker/
COPY nuget.config ./

RUN --mount=type=secret,id=nuget_token,required=false \
    set -e; \
    TOKEN=$(cat /run/secrets/nuget_token 2>/dev/null || true); \
    if [ -n "$TOKEN" ]; then \
      dotnet nuget add source https://nuget.pkg.github.com/Kymr10n/index.json \
        --name github --username Kymr10n --password "$TOKEN" --store-password-in-clear-text; \
    fi; \
    dotnet restore backend/worker/Orkyo.Community.Worker.csproj

COPY backend/ backend/

RUN dotnet publish backend/worker/Orkyo.Community.Worker.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

RUN useradd -r -u 999 -s /bin/false appuser \
    && chown -R appuser:appuser /app
USER appuser

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD pgrep -f Orkyo.Community.Worker.dll || exit 1

ENTRYPOINT ["dotnet", "Orkyo.Community.Worker.dll"]
