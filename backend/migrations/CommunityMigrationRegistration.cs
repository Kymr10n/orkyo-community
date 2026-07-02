using Microsoft.Extensions.DependencyInjection;
using Orkyo.Migrations.Abstractions;

namespace Orkyo.Community.Migrations;

/// <summary>
/// DI extension registering community migration modules.
/// Call after <c>AddOrkyoMigrationPlatform()</c> and <c>AddCommunityFoundationMigrations()</c>.
/// </summary>
public static class CommunityMigrationRegistration
{
    public static IServiceCollection AddCommunityMigrations(this IServiceCollection services)
    {
        services.AddSingleton<IMigrationModule, CommunityMigrationModule>();
        return services;
    }

    /// <summary>
    /// Registers the foundation migration module as consumed by Community — the full foundation
    /// set minus the tenant-phase feedback migrations that would collide in Community's single
    /// shared database. Use this instead of foundation's <c>AddFoundationMigrations()</c>.
    /// </summary>
    public static IServiceCollection AddCommunityFoundationMigrations(this IServiceCollection services)
    {
        services.AddSingleton<IMigrationModule, CommunityFoundationMigrationModule>();
        return services;
    }
}
