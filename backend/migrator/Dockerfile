# syntax=docker/dockerfile:1.4
# Migrator is a one-shot CLI — no HEALTHCHECK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/migrations/Orkyo.Community.Migrations.csproj backend/migrations/
COPY backend/migrator/Orkyo.Community.Migrator.csproj backend/migrator/
COPY nuget.config ./

RUN --mount=type=secret,id=nuget_token,required=true \
    dotnet nuget add source https://nuget.pkg.github.com/Kymr10n/index.json \
      --name github --username Kymr10n \
      --password "$(cat /run/secrets/nuget_token)" \
      --store-password-in-clear-text \
    && dotnet restore backend/migrator/Orkyo.Community.Migrator.csproj -r linux-musl-x64 /p:PublishReadyToRun=true

COPY backend/migrations/ backend/migrations/
COPY backend/migrator/ backend/migrator/

RUN dotnet publish backend/migrator/Orkyo.Community.Migrator.csproj \
    -c Release -o /app/publish --no-restore \
    -r linux-musl-x64 --no-self-contained /p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
LABEL org.opencontainers.image.source=https://github.com/Kymr10n/orkyo-community
WORKDIR /app
COPY --from=build /app/publish .

RUN adduser -D -u 999 -s /sbin/nologin appuser \
    && chown -R appuser:appuser /app
USER appuser

ENTRYPOINT ["dotnet", "Orkyo.Community.Migrator.dll"]
CMD ["migrate", "--target", "all"]
