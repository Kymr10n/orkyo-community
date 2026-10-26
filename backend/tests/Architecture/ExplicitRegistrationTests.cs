namespace Orkyo.Community.Tests.Architecture;

/// <summary>
/// Enforces the explicit-registration rule (CLAUDE.md): if <c>Program.cs</c> calls
/// <c>UseX()</c>, it must also call the matching <c>AddX()</c> in the same file,
/// never relying on <c>AddFoundationServices</c> to register a service the API
/// project uses directly. The rule exists because Foundation is a NuGet package
/// consumed in CI/Docker: an implicit dependency on Foundation registering a
/// service silently breaks during the publish window.
///
/// <para>The classification lives in foundation's <see cref="ExplicitRegistrationContract"/>
/// and is a self-ratchet: any <c>UseX</c> that is neither mapped nor known to need no
/// registration fails the test, forcing whoever adds new middleware to classify it there
/// (or in an edition-specific row passed to <c>Check</c>). Every activation Community's
/// Program.cs makes today is in the shared map.</para>
/// </summary>
public class ExplicitRegistrationTests
{
    [Fact]
    public void EveryMiddleware_HasItsRegistrationInProgram()
    {
        var programPath = ExplicitRegistrationContract.FindProgramCs(AppContext.BaseDirectory);
        programPath.Should().NotBeNull("could not locate backend/api/Program.cs");

        var findings = ExplicitRegistrationContract.Check(File.ReadAllText(programPath!));

        findings.Uses.Should().NotBeEmpty("no app.UseX() calls found — did Program.cs move or the regex break?");
        findings.Unclassified.Should().BeEmpty(findings.ExplainUnclassified());
        findings.Missing.Should().BeEmpty(findings.ExplainMissing());
    }
}
