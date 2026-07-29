namespace Geopilot.Pipeline.Test;

[TestClass]
public class StepResultTest
{
    // SampleResult is private to the test assembly and therefore not accessible from
    // Geopilot.Pipeline, where StepResult performs the reflection. Using it keeps the tests honest:
    // they exercise the same "read a property off a result type the engine cannot see" case that
    // real (internal) plugin result types hit.
    private sealed class SampleResult
    {
        public string? Value { get; init; }

        public int Number { get; init; }
    }

    [TestMethod]
    public void ExtractPropertyReturnsValueForExistingProperty()
    {
        var stepResult = new StepResult { Result = new SampleResult { Value = "hello", Number = 42 } };

        Assert.AreEqual("hello", stepResult.ExtractProperty("Value"));
        Assert.AreEqual(42, stepResult.ExtractProperty("Number"));
    }

    [TestMethod]
    public void ExtractPropertyThrowsForMissingProperty()
    {
        var stepResult = new StepResult { Result = new SampleResult { Value = "hello" } };

        Assert.ThrowsExactly<ArgumentException>(() => stepResult.ExtractProperty("DoesNotExist"));
    }

    [TestMethod]
    public void ExtractPropertyReturnsNullWhenResultIsNull()
    {
        // A synthetic step result (for example a pre-condition status message) has no backing
        // process result, so nothing is resolvable by name and every lookup yields null rather
        // than throwing.
        var stepResult = new StepResult();

        Assert.IsNull(stepResult.ExtractProperty("Value"));
    }

    [TestMethod]
    public void ExtractPropertyReturnsNullWhenPropertyValueIsNull()
    {
        // The property exists and is readable; its value merely happens to be null. This is not an
        // error: a null value is distinct from a missing property, which throws.
        var stepResult = new StepResult { Result = new SampleResult { Value = null } };

        Assert.IsNull(stepResult.ExtractProperty("Value"));
    }

    [TestMethod]
    public void ExtractPropertyIsCaseSensitive()
    {
        var stepResult = new StepResult { Result = new SampleResult { Value = "hello" } };

        // Lookup is case-sensitive: the reference must match the PascalCase property name exactly.
        // A case mismatch is treated as a missing property and throws.
        Assert.ThrowsExactly<ArgumentException>(() => stepResult.ExtractProperty("value"));
    }

    [TestMethod]
    public void ExtractPropertyReadsPropertyOnTypeInaccessibleToEngine()
    {
        // SampleResult is inaccessible from Geopilot.Pipeline, yet reflection reads its property.
        // This is exactly why the engine uses reflection and not `dynamic`: a dynamic access binds
        // against the calling assembly's accessibility and fails on internal plugin result types.
        var stepResult = new StepResult { Result = new SampleResult { Value = "hello" } };

        Assert.AreEqual("hello", stepResult.ExtractProperty("Value"));
    }
}
