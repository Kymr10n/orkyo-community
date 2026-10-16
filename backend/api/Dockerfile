# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
WORKDIR /src

# Central Package Management: every PackageReference is versionless, so restore
# fails with NU1015 without the central version list in the context.
COPY Directory.Build.props Directory.Packages.props ./
COPY backend/migrations/Orkyo.Community.Migrations.csproj backend/migrations/
COPY backend/src/Orkyo.Community.csproj backend/src/
COPY backend/api/Orkyo.Community.Api.csproj backend/api/
# nuget.config declares the `github` source (adding it again would fail); this
# only attaches the credentials, which are never stored in the repo.
COPY nuget.config ./

RUN --mount=type=secret,id=nuget_token,required=true \
    dotnet nuget update source github \
      --username Kymr10n \
      --password "$(cat /run/secrets/nuget_token)" \
      --store-password-in-clear-text \
    && dotnet restore backend/api/Orkyo.Community.Api.csproj -r linux-musl-x64 /p:PublishReadyToRun=true

COPY backend/ backend/

RUN dotnet publish backend/api/Orkyo.Community.Api.csproj \
    -c Release -o /app/publish --no-restore \
    -r linux-musl-x64 --no-self-contained /p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:819bd3649d8bf53f35527c68d62e17b348ef6f24ae73073f497f00a878348062 AS runtime
LABEL org.opencontainers.image.source=https://github.com/Kymr10n/orkyo-community
WORKDIR /app
# The IANA time zone database. Alpine ships without it, and SchedulingEngine resolves
# every site's time zone through TimeZoneInfo — without this package each scheduled
# request write threw TimeZoneNotFoundException and surfaced as a 500. Installing a
# package is compatible with the digest-pinned base (only ad-hoc `apk upgrade` is not);
# the nightly security-refresh rebuild re-resolves this layer with the rest.
# krb5-libs: Npgsql probes GSSAPI on every first connection, and without the library the
# musl loader printed "Error loading shared library libgssapi_krb5.so.2" to stderr at
# startup. Harmless (it fell back to SCRAM), but it read as an error in every log.
RUN apk add --no-cache tzdata krb5-libs
COPY --from=build /app/publish .

RUN adduser -D -u 1001 -s /sbin/nologin appuser \
    && chown -R appuser:appuser /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER appuser

HEALTHCHECK --interval=10s --timeout=3s --retries=5 \
  CMD wget -q --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Orkyo.Community.Api.dll"]
