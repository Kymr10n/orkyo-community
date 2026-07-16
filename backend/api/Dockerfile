# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:548d93f8a18a1acbe6cc127bc4f47281430d34a9e35c18afa80a8d6741c2adc3 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:57bd717ac18ff6c8a39cc0ee4a76c1f15adc46df50434c73eff0c3f1df4c88f0 AS runtime
LABEL org.opencontainers.image.source=https://github.com/Kymr10n/orkyo-community
WORKDIR /app
COPY --from=build /app/publish .

RUN adduser -D -u 1001 -s /sbin/nologin appuser \
    && chown -R appuser:appuser /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER appuser

HEALTHCHECK --interval=10s --timeout=3s --retries=5 \
  CMD wget -q --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Orkyo.Community.Api.dll"]
