# syntax=docker/dockerfile:1.4
# Migrator is a one-shot CLI — no HEALTHCHECK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/migrations/Orkyo.Community.Migrations.csproj backend/migrations/
COPY backend/migrator/Orkyo.Community.Migrator.csproj backend/migrator/
COPY nuget.config ./

RUN --mount=type=secret,id=nuget_token,required=false \
    set -e; \
    TOKEN=$(cat /run/secrets/nuget_token 2>/dev/null || true); \
    if [ -n "$TOKEN" ]; then \
      dotnet nuget add source https://nuget.pkg.github.com/Kymr10n/index.json \
        --name github --username Kymr10n --password "$TOKEN" --store-password-in-clear-text; \
    fi; \
    dotnet restore backend/migrator/Orkyo.Community.Migrator.csproj

COPY backend/migrations/ backend/migrations/
COPY backend/migrator/ backend/migrator/

RUN dotnet publish backend/migrator/Orkyo.Community.Migrator.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

RUN useradd -r -u 999 -s /bin/false appuser \
    && chown -R appuser:appuser /app
USER appuser

ENTRYPOINT ["dotnet", "Orkyo.Community.Migrator.dll"]
