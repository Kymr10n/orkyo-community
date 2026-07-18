using CommandLine;
using Npgsql;
using Orkyo.Foundation.Seed;

namespace Orkyo.Community.Seed;

public sealed class CliOptions : SeedCliOptions
{
    [Option("tenant-id", Default = null,
        HelpText = "Override the single-tenant id used for asset rows. Defaults to Community__TenantId env var, then the community default tenant id.")]
    public string? TenantId { get; init; }

    [Option("connection", Default = null,
        HelpText = "Override the DB connection string. Defaults to ConnectionStrings__DefaultConnection env var.")]
    public string? Connection { get; init; }
}

public static class Program
{
    // Mirror Orkyo.Community.CommunityConfigKeys' env-var forms — this CLI does not reference
    // the Orkyo.Community project, so the env-var names are duplicated here.
    private const string DefaultConnectionEnvVar = "ConnectionStrings__DefaultConnection";
    private const string CommunityTenantIdEnvVar = "Community__TenantId";

    public static async Task<int> Main(string[] args)
    {
        var result = Parser.Default.ParseArguments<CliOptions>(args);
        if (result is not Parsed<CliOptions> parsed) return 1;
        return await RunAsync(parsed.Value);
    }

    private static async Task<int> RunAsync(CliOptions opts)
    {
        if (SeedCliSupport.ValidateProfileAndScale(opts) is { } exitCode) return exitCode;

        var connString = opts.Connection
            ?? Environment.GetEnvironmentVariable(DefaultConnectionEnvVar)
            ?? "Host=localhost;Port=5433;Database=orkyo_community_dev;Username=postgres;Password=postgres";

        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();

        // Single-tenant: assets.tenant_id must match the id the app serves (OrgContext.OrgId =
        // SingleTenantOptions.TenantId, config "Community:TenantId"). Mirror that resolution.
        var tenantId = ResolveTenantId(opts.TenantId);

        var seedOpts = SeedCliSupport.BuildSeedOptions(opts, tenantId);

        Console.WriteLine(
            $"Seeding Community DB ({new NpgsqlConnectionStringBuilder(connString).Database}) — " +
            $"profile={opts.Profile}, scale={opts.Scale}, mode={seedOpts.Mode}, floorplans={opts.Floorplans}, tools={opts.Tools}.");

        try
        {
            var report = await SeedRunner.RunAsync(conn, seedOpts);
            SeedCliSupport.PrintReport(report);
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Seed aborted: {ex.Message}");
            return 4;
        }
    }

    // Mirrors SingleTenantOptions: --tenant-id override, else Community__TenantId env, else default.
    private static Guid ResolveTenantId(string? overrideValue)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue))
            return Guid.Parse(overrideValue);
        var fromEnv = Environment.GetEnvironmentVariable(CommunityTenantIdEnvVar);
        return Guid.TryParse(fromEnv, out var id)
            ? id
            : new Guid("00000000-0000-0000-0000-000000000001");
    }
}
