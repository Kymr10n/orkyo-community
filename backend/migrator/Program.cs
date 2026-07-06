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
    // Mirror Orkyo.Shared.ConfigKeys.ConnectionStringControlPlaneEnvVar / .ControlPlaneConnectionLegacyEnvVar
    // and Orkyo.Community.CommunityConfigKeys' DefaultConnection env-var forms — this standalone
    // migrator references neither project, so the env-var names are duplicated here.
    private const string ControlPlaneEnvVar = "ConnectionStrings__ControlPlane";
    private const string ControlPlaneLegacyEnvVar = "CONTROL_PLANE_CONNECTION_STRING";
    private const string DefaultConnectionEnvVar = "ConnectionStrings__DefaultConnection";
    private const string DefaultConnectionLegacyEnvVar = "DEFAULT_CONNECTION_STRING";

    public static async Task<int> Main(string[] args)
    {
        // Community uses a single database for everything.
        // MigrationCli reads ConnectionStrings__ControlPlane directly from
        // Environment.GetEnvironmentVariable, so we must set the process env var —
        // IConfiguration injection is not sufficient.
        var defaultConn =
            Environment.GetEnvironmentVariable(DefaultConnectionEnvVar)
            ?? Environment.GetEnvironmentVariable(DefaultConnectionLegacyEnvVar);

        if (!string.IsNullOrEmpty(defaultConn))
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ControlPlaneEnvVar)))
                Environment.SetEnvironmentVariable(ControlPlaneEnvVar, defaultConn);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ControlPlaneLegacyEnvVar)))
                Environment.SetEnvironmentVariable(ControlPlaneLegacyEnvVar, defaultConn);
        }

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

        return await services.RunMigrationCliAsync(args);
    }
}
