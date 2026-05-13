# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:8a90a473da5205a16979de99d2fc20975e922c68304f5c79d564e666dc3982fc AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY backend/migrations/Orkyo.Community.Migrations.csproj backend/migrations/
COPY backend/src/Orkyo.Community.csproj backend/src/
COPY backend/api/Orkyo.Community.Api.csproj backend/api/
COPY nuget.config ./

RUN --mount=type=secret,id=nuget_token,required=true \
    dotnet nuget add source https://nuget.pkg.github.com/Kymr10n/index.json \
      --name github --username Kymr10n \
      --password "$(cat /run/secrets/nuget_token)" \
      --store-password-in-clear-text \
    && dotnet restore backend/api/Orkyo.Community.Api.csproj -r linux-musl-x64 /p:PublishReadyToRun=true

COPY backend/ backend/

RUN dotnet publish backend/api/Orkyo.Community.Api.csproj \
    -c Release -o /app/publish --no-restore \
    -r linux-musl-x64 --no-self-contained /p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:60eb031b554df75a4b9f358290a2fa15d8961a3bc79b47bb34a00e31f7b78c69 AS runtime
LABEL org.opencontainers.image.source=https://github.com/Kymr10n/orkyo-community
WORKDIR /app
COPY --from=build /app/publish .

RUN adduser -D -u 1001 -s /sbin/nologin appuser \
    && mkdir -p /app/storage \
    && chown -R appuser:appuser /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER appuser

HEALTHCHECK --interval=10s --timeout=3s --retries=5 \
  CMD wget -q --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Orkyo.Community.Api.dll"]
