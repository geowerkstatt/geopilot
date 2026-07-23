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
    public void TryGetOutputReturnsValueForExistingProperty()
    {
        var stepResult = new StepResult { Result = new SampleResult { Value = "hello", Number = 42 } };

        Assert.IsTrue(stepResult.TryGetOutput("Value", out var value));
        Assert.AreEqual("hello", value);

        Assert.IsTrue(stepResult.TryGetOutput("Number", out var number));
        Assert.AreEqual(42, number);
    }

    [TestMethod]
    public void TryGetOutputReturnsFalseForMissingProperty()
    {
        var stepResult = new StepResult { Result = new SampleResult { Value = "hello" } };

        Assert.IsFalse(stepResult.TryGetOutput("DoesNotExist", out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void TryGetOutputReturnsFalseWhenResultIsNull()
    {
        // A synthetic step result (for example a pre-condition status message) has no backing
        // process result, so nothing is resolvable by name.
        var stepResult = new StepResult();

        Assert.IsFalse(stepResult.TryGetOutput("Value", out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void TryGetOutputReturnsTrueWithNullWhenPropertyValueIsNull()
    {
        // The property exists and is readable; its value merely happens to be null. This must be
        // distinguished from a missing property, which returns false.
        var stepResult = new StepResult { Result = new SampleResult { Value = null } };

        Assert.IsTrue(stepResult.TryGetOutput("Value", out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void TryGetOutputIsCaseSensitive()
    {
        var stepResult = new StepResult { Result = new SampleResult { Value = "hello" } };

        // Lookup is case-sensitive: the reference must match the PascalCase property name exactly.
        Assert.IsFalse(stepResult.TryGetOutput("value", out _));
    }

    [TestMethod]
    public void TryGetOutputReadsPropertyOnTypeInaccessibleToEngine()
    {
        // SampleResult is inaccessible from Geopilot.Pipeline, yet reflection reads its property.
        // This is exactly why the engine uses reflection and not `dynamic`: a dynamic await binds
        // against the calling assembly's accessibility and fails on internal plugin result types.
        var stepResult = new StepResult { Result = new SampleResult { Value = "hello" } };

        Assert.IsTrue(stepResult.TryGetOutput("Value", out var value));
        Assert.AreEqual("hello", value);
    }
}
