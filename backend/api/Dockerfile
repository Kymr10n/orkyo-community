FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src/orkyo-community/backend/api

COPY orkyo-foundation/backend/shared                  /src/orkyo-foundation/backend/shared
COPY orkyo-foundation/backend/src                     /src/orkyo-foundation/backend/src
COPY orkyo-foundation/backend/migration-abstractions  /src/orkyo-foundation/backend/migration-abstractions
COPY orkyo-foundation/backend/migrator-runtime        /src/orkyo-foundation/backend/migrator-runtime
COPY orkyo-foundation/backend/migrations-foundation   /src/orkyo-foundation/backend/migrations-foundation

COPY orkyo-community/backend/migrations  /src/orkyo-community/backend/migrations
COPY orkyo-community/backend/src         /src/orkyo-community/backend/src

COPY orkyo-community/backend/api/Orkyo.Community.Api.csproj  ./
RUN dotnet restore Orkyo.Community.Api.csproj

COPY orkyo-community/backend/api ./
RUN dotnet publish Orkyo.Community.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/storage
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Orkyo.Community.Api.dll"]
