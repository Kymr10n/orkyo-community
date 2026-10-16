# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
WORKDIR /src

# Central Package Management: every PackageReference is versionless, so restore
# fails with NU1015 without the central version list in the context.
COPY Directory.Build.props Directory.Packages.props ./
COPY backend/worker/Orkyo.Community.Worker.csproj backend/worker/
# nuget.config declares the `github` source (adding it again would fail); this
# only attaches the credentials, which are never stored in the repo.
COPY nuget.config ./

RUN --mount=type=secret,id=nuget_token,required=true \
    dotnet nuget update source github \
      --username Kymr10n \
      --password "$(cat /run/secrets/nuget_token)" \
      --store-password-in-clear-text \
    && dotnet restore backend/worker/Orkyo.Community.Worker.csproj -r linux-musl-x64 /p:PublishReadyToRun=true

COPY backend/ backend/

RUN dotnet publish backend/worker/Orkyo.Community.Worker.csproj \
    -c Release -o /app/publish --no-restore \
    -r linux-musl-x64 --no-self-contained /p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine@sha256:e90a4b984926ed14328afec87e9be930b00dba55fed7e1743fbd585022afbad7 AS runtime
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
USER appuser

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD pgrep -f Orkyo.Community.Worker.dll || exit 1

ENTRYPOINT ["dotnet", "Orkyo.Community.Worker.dll"]
