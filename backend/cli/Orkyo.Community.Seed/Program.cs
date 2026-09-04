using Npgsql;
using Orkyo.Foundation.Seed;

namespace Orkyo.Community.Seed;

public sealed class CliOptions : SeedCliOptions
{
    /// <summary>Override the single-tenant id used for asset rows.</summary>
    public string? TenantId { get; set; }

    /// <summary>Override the DB connection string.</summary>
    public string? Connection { get; set; }

    private static readonly string[] OwnOptionNames = ["tenant-id", "connection"];

    public static string[] OptionNames => [.. SharedOptionNames, .. OwnOptionNames];

    public const string HelpText = """
        Usage: orkyo-community-seed --profile <name> [options]

        """ + SharedHelpText + """

          --tenant-id   Override the single-tenant id used for asset rows. Defaults to the
                        Community__TenantId env var, then the community default tenant id.
          --connection  Override the DB connection string. Defaults to the
                        ConnectionStrings__DefaultConnection env var.
        """;

    public static CliOptions Bind(SeedArgs args)
    {
        var options = new CliOptions
        {
            TenantId = args.String("tenant-id"),
            Connection = args.String("connection"),
        };
        options.BindShared(args);
        return options;
    }
}

public static class Program
{
    // Mirror Orkyo.Community.CommunityConfigKeys' env-var forms — this CLI does not reference
    // the Orkyo.Community project, so the env-var names are duplicated here.
    private const string DefaultConnectionEnvVar = "ConnectionStrings__DefaultConnection";
    private const string CommunityTenantIdEnvVar = "Community__TenantId";

    public static async Task<int> Main(string[] args)
    {
        if (SeedCliSupport.IsHelpRequested(args))
        {
            Console.WriteLine(CliOptions.HelpText);
            return 0;
        }

        var parsed = SeedArgs.Parse(args, CliOptions.OptionNames, out var error);
        if (parsed is null)
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliOptions.HelpText);
            return 1;
        }
        return await RunAsync(CliOptions.Bind(parsed));
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
            $"profile={opts.Profile}, scale={opts.Scale}, mode={seedOpts.Mode}, floorplans={opts.Floorplans}.");

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
