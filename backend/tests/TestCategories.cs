namespace Orkyo.Community.Tests;

/// <summary>
/// The xUnit trait on every test class that needs a database (the fixture collections), so
/// <c>check --fast</c> can run everything else with <c>--filter "Category!=Database"</c>.
/// A local copy of foundation's <c>Orkyo.Foundation.TestSupport.TestCategories</c>; the next
/// foundation bump replaces it.
/// </summary>
public static class TestCategories
{
    public const string Name = "Category";
    public const string Database = "Database";
}
