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
