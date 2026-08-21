namespace Geopilot.Api;

/// <summary>
/// Test data generation lives in Geopilot.Api.SeedData, which references the API and never the other way
/// round, so that neither the seed code nor Bogus reaches the published API. A reference back would be
/// circular and fail the build, so this test covers what the compiler does not: Bogus reappearing as a
/// dependency of the API.
/// </summary>
/// <remarks>
/// The check reads the assembly references the compiler emitted, which are only the ones the API actually
/// uses. A package reference that is present but unused, or one that arrives transitively through another
/// project, still reaches the publish output while this test stays green.
/// </remarks>
[TestClass]
public sealed class DependencyBoundaryTest
{
    [TestMethod]
    public void ApiDoesNotDependOnTestDataGeneration()
    {
        var forbidden = new[] { "Bogus", "Geopilot.Api.SeedData" };

        var violations = typeof(Context).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .OfType<string>()
            .Intersect(forbidden, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEquivalent(
            Array.Empty<string>(),
            violations,
            "Geopilot.Api must not depend on test data generation. Move the code to Geopilot.Api.SeedData instead.");
    }
}
