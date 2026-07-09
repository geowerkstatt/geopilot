using Geopilot.Pipeline.Config;
using System.Reflection;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class InputBinderTest
{
    private static readonly StepOutputResolver EmptyResolver = (string stepId, string outputName, out object? value) =>
    {
        value = null;
        return false;
    };

    [TestMethod]
    public void BindsLiteralStringToString()
    {
        var result = InputBinder.Bind(Single(typeof(string)), new InputValue.Literal("hello"), EmptyResolver);

        Assert.AreEqual("hello", result);
    }

    [TestMethod]
    public void ConvertsLiteralToNumber()
    {
        var result = InputBinder.Bind(Single(typeof(int)), new InputValue.Literal("100"), EmptyResolver);

        Assert.AreEqual(100, result);
    }

    [TestMethod]
    public void ConvertsLiteralToBool()
    {
        var result = (bool)InputBinder.Bind(Single(typeof(bool)), new InputValue.Literal("true"), EmptyResolver)!;

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ResolvesStepOutputReference()
    {
        var result = InputBinder.Bind(Single(typeof(int)), new InputValue.StepOutputReference("validation", "count"), ResolverReturning(42));

        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public void ThrowsWhenStepOutputReferenceCannotBeResolved()
    {
        var exception = Assert.Throws<PipelineRunException>(
            () => InputBinder.Bind(Single(typeof(string)), new InputValue.StepOutputReference("ghost", "out"), EmptyResolver));

        Assert.Contains("ghost.out", exception.Message);
    }

    [TestMethod]
    public void MissingInputForNullableParameterBindsNull()
    {
        var result = InputBinder.Bind(Single(typeof(string), nullable: true), null, EmptyResolver);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void MissingInputForNonNullableParameterThrows()
    {
        Assert.Throws<PipelineRunException>(
            () => InputBinder.Bind(Single(typeof(string)), null, EmptyResolver));
    }

    [TestMethod]
    public void ExplicitNullForNonNullableParameterThrows()
    {
        Assert.Throws<PipelineRunException>(
            () => InputBinder.Bind(Single(typeof(string)), new InputValue.Literal(null), EmptyResolver));
    }

    [TestMethod]
    public void UnwrapsSingleElementListIntoSingleValue()
    {
        var result = InputBinder.Bind(Single(typeof(string)), new InputValue.StepOutputReference("s", "o"), ResolverReturning(new[] { "only" }));

        Assert.AreEqual("only", result);
    }

    [TestMethod]
    public void UnwrapsSingleNullElementListIntoNull()
    {
        var result = InputBinder.Bind(Single(typeof(string), nullable: true), new InputValue.StepOutputReference("s", "o"), ResolverReturning(new object?[] { null }));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ThrowsWhenListWithManyElementsBindsToSingleValue()
    {
        Assert.Throws<PipelineRunException>(
            () => InputBinder.Bind(Single(typeof(string)), new InputValue.StepOutputReference("s", "o"), ResolverReturning(new[] { "a", "b" })));
    }

    [TestMethod]
    public void EmptyListForNullableSingleValueBindsNull()
    {
        var result = InputBinder.Bind(Single(typeof(string), nullable: true), new InputValue.StepOutputReference("s", "o"), ResolverReturning(Array.Empty<string>()));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void BindsListToArrayParameter()
    {
        var result = InputBinder.Bind(ArrayTarget(typeof(string[])), new InputValue.StepOutputReference("s", "o"), ResolverReturning(new[] { "a", "b" }));

        CollectionAssert.AreEqual(new[] { "a", "b" }, (string[])result!);
    }

    [TestMethod]
    public void WrapsSingleValueIntoArrayOfOne()
    {
        var result = InputBinder.Bind(ArrayTarget(typeof(string[])), new InputValue.Literal("x"), EmptyResolver);

        CollectionAssert.AreEqual(new[] { "x" }, (string[])result!);
    }

    [TestMethod]
    public void ConvertsArrayElementsToElementType()
    {
        var result = InputBinder.Bind(ArrayTarget(typeof(int[])), new InputValue.StepOutputReference("s", "o"), ResolverReturning(new[] { "1", "2" }));

        CollectionAssert.AreEqual(new[] { 1, 2 }, (int[])result!);
    }

    [TestMethod]
    public void ThrowsWhenArrayElementIsNullAndElementNotNullable()
    {
        Assert.Throws<PipelineRunException>(
            () => InputBinder.Bind(ArrayTarget(typeof(string[])), new InputValue.StepOutputReference("s", "o"), ResolverReturning(new object?[] { "a", null })));
    }

    [TestMethod]
    public void AllowsNullArrayElementWhenElementNullable()
    {
        var result = InputBinder.Bind(ArrayTarget(typeof(int?[]), elementNullable: true), new InputValue.StepOutputReference("s", "o"), ResolverReturning(new object?[] { null, 5 }));

        CollectionAssert.AreEqual(new int?[] { null, 5 }, (int?[])result!);
    }

    [TestMethod]
    public void ThrowsWhenValueCannotBeConverted()
    {
        Assert.Throws<PipelineRunException>(
            () => InputBinder.Bind(Single(typeof(bool)), new InputValue.Literal("not a bool"), EmptyResolver));
    }

    [TestMethod]
    public void FromParameterReadsNullability()
    {
        var parameters = typeof(InputBinderTest)
            .GetMethod(nameof(SampleParameters), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

        var nonNullableInt = BindingTarget.FromParameter(parameters[0]);
        var nullableInt = BindingTarget.FromParameter(parameters[1]);
        var nullableStrings = BindingTarget.FromParameter(parameters[2]);
        var nonNullableStrings = BindingTarget.FromParameter(parameters[3]);

        Assert.IsFalse(nonNullableInt.IsNullable);
        Assert.IsTrue(nullableInt.IsNullable);
        Assert.IsTrue(nullableStrings.IsElementNullable);
        Assert.IsFalse(nonNullableStrings.IsElementNullable);
    }

    private static BindingTarget Single(Type type, bool nullable = false) => new("param", type, nullable, false);

    private static BindingTarget ArrayTarget(Type type, bool elementNullable = false) => new("param", type, false, elementNullable);

    private static StepOutputResolver ResolverReturning(object? value) =>
        (string stepId, string outputName, out object? resolved) =>
        {
            resolved = value;
            return true;
        };

    private static void SampleParameters(int nonNullableInt, int? nullableInt, string?[] nullableStrings, string[] nonNullableStrings)
    {
    }
}
