using System.Text.RegularExpressions;

namespace Orkyo.Community.Tests.Architecture;

/// <summary>
/// Enforces the explicit-registration rule (CLAUDE.md): if <c>Program.cs</c> calls
/// <c>UseX()</c>, it must also call the matching <c>AddX()</c> in the same file,
/// never relying on <c>AddFoundationServices</c> to register a service the API
/// project uses directly. The rule exists because Foundation is a NuGet package
/// consumed in CI/Docker: an implicit dependency on Foundation registering a
/// service silently breaks during the publish window.
///
/// <para>The <see cref="UseToAdd"/> map is a self-ratchet: any <c>UseX</c> that is
/// neither mapped nor in <see cref="NoRegistrationNeeded"/> nor
/// <see cref="KnownTransitionalExceptions"/> fails the test, forcing whoever adds
/// new middleware to classify it here.</para>
///
/// See orkyo-infra/docs/optimization-plan-2026-07.md §Guardrails (G7).
///
/// Keep in sync with the SaaS counterpart (orkyo-saas/backend/tests/Architecture/
/// ExplicitRegistrationTests.cs). The two deliberately differ where the editions'
/// registrations differ, so they are NOT G4-manifest-synced — port structural
/// improvements both ways.
/// </summary>
public partial class ExplicitRegistrationTests
{
    /// <summary>
    /// Middleware activation -> the DI registration(s) that satisfy it (any one is
    /// enough). Superset shared with saas; entries for middleware this project
    /// doesn't use are simply never exercised.
    /// </summary>
    private static readonly Dictionary<string, string[]> UseToAdd = new()
    {
        ["UseAuthentication"] = ["AddOrkyoAuthentication", "AddAuthentication"],
        ["UseAuthorization"] = ["AddAuthorization"],
        ["UseCors"] = ["AddOrkyoApiCors", "AddCors"],
        // Community registers the limiter via AddFoundationRateLimiting; saas via
        // AddOrkyoRateLimiting (AddOrkYoRateLimiting until the W1.8 rename).
        ["UseRateLimiter"] = ["AddFoundationRateLimiting", "AddOrkyoRateLimiting", "AddOrkYoRateLimiting", "AddRateLimiter"],
        ["UseAuthenticatedRateLimiting"] = ["AddOrkyoRateLimiting", "AddOrkYoRateLimiting"],
        ["UseBotProtectionRateLimiting"] = ["AddBotProtection"],
        ["UseFoundationMiddleware"] = ["AddFoundationServices"],
        ["UseResponseCompression"] = ["AddResponseCompression"],
        ["UseOrkyoReportingSwaggerUI"] = ["AddOrkyoReportingSwagger"],
    };

    /// <summary>Middleware with no same-file DI registration requirement.</summary>
    private static readonly HashSet<string> NoRegistrationNeeded =
    [
        "UseRouting",
        "UseHttpsRedirection",
        // Foundation's opt-in Prometheus helper (wraps prometheus-net UseHttpMetrics);
        // the registry is process-wide static state — there is nothing to register.
        "UseOrkyoMetrics",
    ];

    /// <summary>Documented, tracked deviations. Currently empty.</summary>
    private static readonly HashSet<string> KnownTransitionalExceptions = [];

    [GeneratedRegex(@"\bapp\.(Use[A-Za-z0-9]+)\(")]
    private static partial Regex UseCallRegex();

    [Fact]
    public void EveryMiddleware_HasItsRegistrationInProgram()
    {
        var programPath = FindProgramCs();
        programPath.Should().NotBeNull("could not locate backend/api/Program.cs");

        var content = File.ReadAllText(programPath!);

        var uses = UseCallRegex().Matches(content)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .OrderBy(u => u, StringComparer.Ordinal)
            .ToList();

        uses.Should().NotBeEmpty("no app.UseX() calls found — did Program.cs move or the regex break?");

        var unclassified = uses
            .Where(u => !UseToAdd.ContainsKey(u)
                     && !NoRegistrationNeeded.Contains(u)
                     && !KnownTransitionalExceptions.Contains(u))
            .ToList();

        unclassified.Should().BeEmpty(
            "these middleware activations are unclassified — add each to UseToAdd (with its " +
            "AddX), NoRegistrationNeeded, or KnownTransitionalExceptions in " +
            "ExplicitRegistrationTests:\n  " + string.Join("\n  ", unclassified));

        var missing = uses
            .Where(u => UseToAdd.TryGetValue(u, out var _))
            .Where(u => !UseToAdd[u].Any(add =>
                Regex.IsMatch(content, $@"\.{Regex.Escape(add)}\s*\(")))
            .Select(u => $"{u} (expected one of: {string.Join(", ", UseToAdd[u])})")
            .ToList();

        missing.Should().BeEmpty(
            "these middleware are activated but their AddX() is not called in the same Program.cs " +
            "(explicit-registration rule):\n  " + string.Join("\n  ", missing));
    }

    private static string? FindProgramCs()
    {
        var apiDir = FindDirectory("backend", "api");
        if (apiDir == null) return null;
        var path = Path.Combine(apiDir, "Program.cs");
        return File.Exists(path) ? path : null;
    }

    private static string? FindDirectory(params string[] pathSegments)
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            var candidate = Path.Combine([dir, .. pathSegments]);
            if (Directory.Exists(candidate)) return candidate;

            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == null) break;
            dir = parent;
        }
        return null;
    }
}
