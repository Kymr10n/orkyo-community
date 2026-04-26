FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src/orkyo-community/backend/migrator

COPY orkyo-foundation/backend/shared                  /src/orkyo-foundation/backend/shared
COPY orkyo-foundation/backend/migration-abstractions  /src/orkyo-foundation/backend/migration-abstractions
COPY orkyo-foundation/backend/migrator-runtime        /src/orkyo-foundation/backend/migrator-runtime
COPY orkyo-foundation/backend/migrations-foundation   /src/orkyo-foundation/backend/migrations-foundation

COPY orkyo-community/backend/migrations  /src/orkyo-community/backend/migrations

COPY orkyo-community/backend/migrator/Orkyo.Community.Migrator.csproj  ./
RUN dotnet restore Orkyo.Community.Migrator.csproj

COPY orkyo-community/backend/migrator ./
RUN dotnet publish Orkyo.Community.Migrator.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Orkyo.Community.Migrator.dll"]
