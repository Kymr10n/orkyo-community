using Microsoft.Extensions.DependencyInjection;
using Orkyo.Migrations.Abstractions;

namespace Orkyo.Community.Migrations;

/// <summary>
/// DI extension registering community migration modules.
/// Call after <c>AddOrkyoMigrationPlatform()</c> and <c>AddFoundationMigrations()</c>.
/// </summary>
public static class CommunityMigrationRegistration
{
    public static IServiceCollection AddCommunityMigrations(this IServiceCollection services)
    {
        services.AddSingleton<IMigrationModule, CommunityMigrationModule>();
        return services;
    }
}
