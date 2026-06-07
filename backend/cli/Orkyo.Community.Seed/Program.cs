using CommandLine;
using Npgsql;
using Orkyo.Foundation.Seed;
using Orkyo.Foundation.Seed.Profiles;
using Orkyo.Foundation.Seed.Scales;

namespace Orkyo.Community.Seed;

public sealed class CliOptions
{
    [Option("profile", Required = true,
        HelpText = "Required. One of: generic, manufacturing, construction, camping, education.")]
    public string Profile { get; init; } = "";

    [Option("scale", Default = "medium",
        HelpText = "One of: tiny, small, medium, large, xlarge.")]
    public string Scale { get; init; } = "medium";

    [Option("mode", Default = "reset",
        HelpText = "reset (truncate tables before seeding) or append.")]
    public string Mode { get; init; } = "reset";

    [Option("seed", Default = 1337,
        HelpText = "Random seed for deterministic generation.")]
    public int RandomSeed { get; init; } = 1337;

    [Option("random", Default = false,
        HelpText = "Use a fresh random seed instead of the fixed --seed value.")]
    public bool UseRandom { get; init; }

    [Option("force-non-local", Default = false,
        HelpText = "Override the safety guard that refuses non-local connections.")]
    public bool ForceNonLocal { get; init; }

    [Option("floorplans", Default = false,
        HelpText = "Seed the curated floorplan-backed sites (with image assets + geometry-bearing spaces) instead of scale-driven sites/spaces. Requires a profile with a floorplan set (manufacturing).")]
    public bool Floorplans { get; init; }

    [Option("tenant-id", Default = null,
        HelpText = "Override the single-tenant id used for asset rows. Defaults to Community__TenantId env var, then the community default tenant id.")]
    public string? TenantId { get; init; }

    [Option("connection", Default = null,
        HelpText = "Override the DB connection string. Defaults to ConnectionStrings__DefaultConnection env var.")]
    public string? Connection { get; init; }
}

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var result = Parser.Default.ParseArguments<CliOptions>(args);
        if (result is not Parsed<CliOptions> parsed) return 1;
        return await RunAsync(parsed.Value);
    }

    private static async Task<int> RunAsync(CliOptions opts)
    {
        try { _ = ProfileCatalog.Resolve(opts.Profile); _ = ScaleCatalog.Resolve(opts.Scale); }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        var connString = opts.Connection
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=orkyo_community_dev;Username=postgres;Password=postgres";

        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();

        // Single-tenant: assets.tenant_id must match the id the app serves (OrgContext.OrgId =
        // SingleTenantOptions.TenantId, config "Community:TenantId"). Mirror that resolution.
        var tenantId = ResolveTenantId(opts.TenantId);

        var seedOpts = new SeedOptions
        {
            Profile = opts.Profile,
            Scale = opts.Scale,
            Mode = opts.Mode.Equals("append", StringComparison.OrdinalIgnoreCase) ? SeedMode.Append : SeedMode.Reset,
            RandomSeed = opts.RandomSeed,
            UseRandom = opts.UseRandom,
            ForceNonLocal = opts.ForceNonLocal,
            UseFloorplans = opts.Floorplans,
            TenantId = tenantId,
        };

        Console.WriteLine(
            $"Seeding Community DB ({new NpgsqlConnectionStringBuilder(connString).Database}) — " +
            $"profile={opts.Profile}, scale={opts.Scale}, mode={seedOpts.Mode}, floorplans={opts.Floorplans}.");

        try
        {
            var report = await SeedRunner.RunAsync(conn, seedOpts);
            Console.WriteLine();
            Console.WriteLine($"Seeded in {report.Duration.TotalSeconds:F1}s:");
            Console.WriteLine($"  Sites:              {report.Sites,8}");
            Console.WriteLine($"  Spaces:             {report.Spaces,8}");
            Console.WriteLine($"  Floorplan assets:   {report.FloorplanAssets,8}");
            Console.WriteLine($"  Job titles:         {report.JobTitles,8}");
            Console.WriteLine($"  Departments:        {report.Departments,8}");
            Console.WriteLine($"  People:             {report.People,8}");
            Console.WriteLine($"  Person groups:      {report.PersonGroups,8}");
            Console.WriteLine($"  Group members:      {report.PersonGroupMembers,8}");
            Console.WriteLine($"  Requests:           {report.Requests,8}");
            Console.WriteLine($"  Assignments:        {report.Assignments,8}");
            if (report.Tools + report.Capabilities + report.AvailabilityEvents + report.Templates > 0)
            {
                Console.WriteLine($"  Tools:              {report.Tools,8}");
                Console.WriteLine($"  Capabilities:       {report.Capabilities,8}");
                Console.WriteLine($"  Requirements:       {report.Requirements,8}");
                Console.WriteLine($"  Availability events:{report.AvailabilityEvents,8}");
                Console.WriteLine($"  Absences:           {report.Absences,8}");
                Console.WriteLine($"  Templates:          {report.Templates,8}");
                Console.WriteLine($"  Conflicts (seeded): {report.Conflicts,8}");
            }
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
        var fromEnv = Environment.GetEnvironmentVariable("Community__TenantId");
        return Guid.TryParse(fromEnv, out var id)
            ? id
            : new Guid("00000000-0000-0000-0000-000000000001");
    }
}
