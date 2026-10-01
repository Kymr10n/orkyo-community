using System.Text.RegularExpressions;

namespace Orkyo.Community.Tests.Architecture;

/// <summary>
/// The Keycloak image this repo builds on is one artifact of a foundation release, like the
/// NuGet and npm packages, and must be the one for the pinned version. It once pointed at a
/// floating tag that nothing refreshed, so a fixed Keycloak could not reach this repo and
/// nobody could see that from the repo itself.
///
/// scripts/bump-foundation.sh writes all three pins in one commit. These tests are the other
/// half: a package bump that leaves the image behind, a hand edit, or a CI override fails
/// here, on every pull request.
/// </summary>
public partial class KeycloakBasePinTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        for (var dir = AppContext.BaseDirectory; dir != null; dir = Directory.GetParent(dir)?.FullName)
        {
            if (File.Exists(Path.Combine(dir, "Directory.Build.props"))
                && Directory.Exists(Path.Combine(dir, "backend", "keycloak")))
                return dir;
        }
        throw new InvalidOperationException("Repository root not found above " + AppContext.BaseDirectory);
    }

    private static string PinnedFoundationVersion()
    {
        var props = File.ReadAllText(Path.Combine(RepoRoot, "Directory.Build.props"));
        var match = FoundationVersion().Match(props);
        match.Success.Should().BeTrue("Directory.Build.props declares <OrkyoFoundationVersion>");
        return match.Groups[1].Value;
    }

    private static string KeycloakBaseImage()
    {
        var dockerfile = File.ReadAllLines(Path.Combine(RepoRoot, "backend", "keycloak", "Dockerfile"));
        var line = dockerfile.Should().ContainSingle(l => l.StartsWith("ARG FOUNDATION_KC_IMAGE=", StringComparison.Ordinal),
            "the Dockerfile names the base image exactly once").Which;
        return line["ARG FOUNDATION_KC_IMAGE=".Length..];
    }

    [Fact]
    public void TheKeycloakBase_IsTheFoundationImageForThePinnedVersion()
    {
        var match = KeycloakImage().Match(KeycloakBaseImage());

        match.Success.Should().BeTrue(
            "the base must be ghcr.io/kymr10n/keycloak:<keycloak line>-orkyo-<version>@sha256:<digest>; got '{0}'",
            KeycloakBaseImage());
        match.Groups["version"].Value.Should().Be(PinnedFoundationVersion(),
            "the Keycloak image and the foundation packages are one release — run scripts/bump-foundation.sh, which moves them together");
    }

    [Fact]
    public void TheKeycloakBase_IsPinnedByDigest()
    {
        // A tag can be re-pointed; a digest cannot. Foundation re-tags images on carry-forward.
        KeycloakBaseImage().Should().MatchRegex("@sha256:[0-9a-f]{64}$");
    }

    [Fact]
    public void NoWorkflowOverridesTheKeycloakBase()
    {
        // A build arg in CI would win over the Dockerfile and bring back a second, unguarded
        // place the base is named — which is how the stale reference survived before.
        var workflows = Directory.GetFiles(Path.Combine(RepoRoot, ".github", "workflows"), "*.yml");

        workflows.Should().NotBeEmpty();
        // An assignment, not a mention: a comment may explain the ARG, a build arg sets it.
        foreach (var workflow in workflows)
            File.ReadAllText(workflow).Should().NotContain("FOUNDATION_KC_IMAGE=",
                "{0} must not pass FOUNDATION_KC_IMAGE; the Dockerfile default is the single source", Path.GetFileName(workflow));
    }

    [GeneratedRegex(@"<OrkyoFoundationVersion[^>]*>([^<]+)</OrkyoFoundationVersion>")]
    private static partial Regex FoundationVersion();

    [GeneratedRegex(@"\Aghcr\.io/kymr10n/keycloak:\d+\.\d+-orkyo-(?<version>[0-9A-Za-z.\-]+)@sha256:[0-9a-f]{64}\z")]
    private static partial Regex KeycloakImage();
}
