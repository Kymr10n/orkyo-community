# syntax=docker/dockerfile:1.4
# Migrator is a one-shot CLI — no HEALTHCHECK.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1fc6e423f543119c406d24e2e687d67c569f18f04a37a8b0005d80ad0dcee80 AS build
WORKDIR /src

# Central Package Management: every PackageReference is versionless, so restore
# fails with NU1015 without the central version list in the context.
COPY Directory.Build.props Directory.Packages.props ./
COPY backend/migrations/Orkyo.Community.Migrations.csproj backend/migrations/
COPY backend/migrator/Orkyo.Community.Migrator.csproj backend/migrator/
# nuget.config declares the `github` source (adding it again would fail); this
# only attaches the credentials, which are never stored in the repo.
COPY nuget.config ./

RUN --mount=type=secret,id=nuget_token,required=true \
    dotnet nuget update source github \
      --username Kymr10n \
      --password "$(cat /run/secrets/nuget_token)" \
      --store-password-in-clear-text \
    && dotnet restore backend/migrator/Orkyo.Community.Migrator.csproj -r linux-musl-x64 /p:PublishReadyToRun=true

COPY backend/migrations/ backend/migrations/
COPY backend/migrator/ backend/migrator/

RUN dotnet publish backend/migrator/Orkyo.Community.Migrator.csproj \
    -c Release -o /app/publish --no-restore \
    -r linux-musl-x64 --no-self-contained /p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine@sha256:216f4e2027da6ae806e0bc4b448669ac0faa00125908e308f31dd70598e58136 AS runtime
LABEL org.opencontainers.image.source=https://github.com/Kymr10n/orkyo-community
WORKDIR /app
COPY --from=build /app/publish .

RUN adduser -D -u 1001 -s /sbin/nologin appuser \
    && chown -R appuser:appuser /app
USER appuser

ENTRYPOINT ["dotnet", "Orkyo.Community.Migrator.dll"]
CMD ["migrate", "--target", "all"]
