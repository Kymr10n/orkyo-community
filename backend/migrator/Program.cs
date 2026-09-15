using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orkyo.Community.Migrations;
using Orkyo.Foundation.Migrations;
using Orkyo.Migrations.Abstractions;
using Orkyo.Migrator;

namespace Orkyo.Community.Migrator;

public static class Program
{
    // Community runs one database for everything. The migrator's control-plane connection
    // is read from the Community keys and handed to MigrationCli as options — no process
    // environment mutation, no duplicated control-plane key names.
    private const string DefaultConnectionEnvVar = "ConnectionStrings__DefaultConnection";
    private const string DefaultConnectionLegacyEnvVar = "DEFAULT_CONNECTION_STRING";

    public static async Task<int> Main(string[] args)
    {
        var defaultConn =
            Environment.GetEnvironmentVariable(DefaultConnectionEnvVar)
            ?? Environment.GetEnvironmentVariable(DefaultConnectionLegacyEnvVar)
            ?? throw new InvalidOperationException(
                $"{DefaultConnectionEnvVar} (or {DefaultConnectionLegacyEnvVar}) is required: the Community migrator has one database.");

        // The same environment rules the SaaS migrator applies (APP_VERSION, lock timeout),
        // with the control-plane connection answered from the Community key.
        var options = MigrationCliOptions.FromEnvironment(key =>
            key == MigrationCliOptions.ConnectionStringEnvVar ? defaultConn : Environment.GetEnvironmentVariable(key));

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        await using var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information))
            .AddOrkyoMigrationPlatform()
            .AddCommunityFoundationMigrations()
            .AddCommunityMigrations()
            .AddSingleton<ITenantRegistry, CommunityTenantRegistry>()
            .BuildServiceProvider();

        return await services.RunMigrationCliAsync(args, options);
    }
}
