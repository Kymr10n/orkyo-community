using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orkyo.Community.Migrations;
using Orkyo.Foundation.Migrations;
using Orkyo.Migrator;
using Orkyo.Migrations.Abstractions;

namespace Orkyo.Community.Migrator;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Community uses a single database for everything.
        // MigrationCli reads ConnectionStrings__ControlPlane directly from
        // Environment.GetEnvironmentVariable, so we must set the process env var —
        // IConfiguration injection is not sufficient.
        var defaultConn =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION_STRING");

        if (!string.IsNullOrEmpty(defaultConn))
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConnectionStrings__ControlPlane")))
                Environment.SetEnvironmentVariable("ConnectionStrings__ControlPlane", defaultConn);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONTROL_PLANE_CONNECTION_STRING")))
                Environment.SetEnvironmentVariable("CONTROL_PLANE_CONNECTION_STRING", defaultConn);
        }

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        await using var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information))
            .AddOrkyoMigrationPlatform()
            .AddFoundationMigrations()
            .AddCommunityMigrations()
            .AddSingleton<ITenantRegistry, CommunityTenantRegistry>()
            .BuildServiceProvider();

        return await services.RunMigrationCliAsync(args);
    }
}
