namespace Geopilot.Api;

/// <summary>
/// Test data generation lives in Geopilot.Api.SeedData, which references the API and never the other way
/// round, so that neither the seed code nor Bogus reaches the published API. Nothing in the build enforces
/// that direction, so this test does.
/// </summary>
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
