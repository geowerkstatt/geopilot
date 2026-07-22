using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Moq;
using System.Reflection;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class InputBinderTest
{
    private static readonly ReferenceResolver EmptyResolver = (InputValue reference, out object? value) =>
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
    public void BindsLiteralSequenceToArray()
    {
        var sequence = new InputValue.Sequence([new InputValue.Literal("xtf"), new InputValue.Literal("ili")]);

        var result = InputBinder.Bind(ArrayTarget(typeof(string[])), sequence, EmptyResolver);

        CollectionAssert.AreEqual(new[] { "xtf", "ili" }, (string[])result!);
    }

    [TestMethod]
    public void BindsListToEnumerableParameter()
    {
        var result = InputBinder.Bind(ListTarget(typeof(IEnumerable<string>)), new InputValue.StepOutputReference("s", "o"), ResolverReturning(new[] { "a", "b" }));

        CollectionAssert.AreEqual(new[] { "a", "b" }, ((IEnumerable<string>)result!).ToArray());
    }

    [TestMethod]
    public void WrapsSingleValueIntoEnumerableOfOne()
    {
        var result = InputBinder.Bind(ListTarget(typeof(IEnumerable<string>)), new InputValue.Literal("x"), EmptyResolver);

        CollectionAssert.AreEqual(new[] { "x" }, ((IEnumerable<string>)result!).ToArray());
    }

    [TestMethod]
    public void SpreadsReferencedListWhileAddingScalarsInSequence()
    {
        // Mirrors the spec "allMessages": literals are added, a referenced list<string> is spread.
        var sequence = new InputValue.Sequence(
        [
            new InputValue.Literal("start"),
            new InputValue.StepOutputReference("validation", "messages"),
            new InputValue.Literal("done"),
        ]);

        var result = InputBinder.Bind(ArrayTarget(typeof(string[])), sequence, ResolverReturning(new[] { "m1", "m2" }));

        CollectionAssert.AreEqual(new[] { "start", "m1", "m2", "done" }, (string[])result!);
    }

    [TestMethod]
    public void UsesReferencedListOfListsAsRows()
    {
        // Mirrors the spec "matrix": a list<list<string>> reference fills a list<list<string>> as is.
        var tables = new[] { new[] { "a", "b" }, new[] { "c" } };

        var result = InputBinder.Bind(ListTarget(typeof(string[][])), new InputValue.StepOutputReference("g", "tables"), ResolverReturning(tables));

        var rows = (string[][])result!;
        Assert.HasCount(2, rows);
        CollectionAssert.AreEqual(new[] { "a", "b" }, rows[0]);
        CollectionAssert.AreEqual(new[] { "c" }, rows[1]);
    }

    [TestMethod]
    public void WrapsReferencedListIntoASingleRow()
    {
        // Mirrors the spec "oneRow": a list<string> reference becomes one row of a list<list<string>>.
        var result = InputBinder.Bind(ListTarget(typeof(string[][])), new InputValue.StepOutputReference("g", "codes"), ResolverReturning(new[] { "a", "b" }));

        var rows = (string[][])result!;
        Assert.HasCount(1, rows);
        CollectionAssert.AreEqual(new[] { "a", "b" }, rows[0]);
    }

    [TestMethod]
    public void CombinesRowsAddingAListAndSpreadingAListOfLists()
    {
        // Mirrors the spec "combinedRows": codes is added as one row, tables is spread into rows.
        ReferenceResolver resolver = (InputValue reference, out object? value) =>
        {
            var outputName = ((InputValue.StepOutputReference)reference).OutputName;
            value = outputName == "codes"
                ? new[] { "a", "b" }
                : new[] { new[] { "x" }, new[] { "y", "z" } };
            return true;
        };
        var sequence = new InputValue.Sequence(
        [
            new InputValue.StepOutputReference("g", "codes"),
            new InputValue.StepOutputReference("g", "tables"),
        ]);

        var result = InputBinder.Bind(ListTarget(typeof(string[][])), sequence, resolver);

        var rows = (string[][])result!;
        Assert.HasCount(3, rows);
        CollectionAssert.AreEqual(new[] { "a", "b" }, rows[0]);
        CollectionAssert.AreEqual(new[] { "x" }, rows[1]);
        CollectionAssert.AreEqual(new[] { "y", "z" }, rows[2]);
    }

    [TestMethod]
    public void ThrowsWhenScalarCannotBecomeListOfLists()
    {
        Assert.Throws<PipelineRunException>(
            () => InputBinder.Bind(ListTarget(typeof(string[][])), new InputValue.Literal("hello"), EmptyResolver));
    }

    [TestMethod]
    public void ThrowsWhenSequenceItemFitsNeitherElementNorList()
    {
        // Mirrors the spec "mixedCodes": a complex object is neither a string nor a list of strings.
        var sequence = new InputValue.Sequence([new InputValue.Literal("ok"), new InputValue.StepOutputReference("v", "stats")]);

        Assert.Throws<PipelineRunException>(
            () => InputBinder.Bind(ArrayTarget(typeof(string[])), sequence, ResolverReturning(new ExampleComplexObject())));
    }

    [TestMethod]
    public void UnwrapsSingleElementSequenceIntoSingleValue()
    {
        var sequence = new InputValue.Sequence([new InputValue.Literal("only")]);

        var result = InputBinder.Bind(Single(typeof(string)), sequence, EmptyResolver);

        Assert.AreEqual("only", result);
    }

    [TestMethod]
    public void ThrowsWhenMultiElementSequenceBindsToSingleValue()
    {
        var sequence = new InputValue.Sequence([new InputValue.Literal("a"), new InputValue.Literal("b")]);

        Assert.Throws<PipelineRunException>(
            () => InputBinder.Bind(Single(typeof(string)), sequence, EmptyResolver));
    }

    [TestMethod]
    public void AllowsNullSequenceItemWhenElementNullable()
    {
        var sequence = new InputValue.Sequence([new InputValue.Literal("a"), new InputValue.Literal(null)]);

        var result = InputBinder.Bind(ArrayTarget(typeof(string[]), elementNullable: true), sequence, EmptyResolver);

        CollectionAssert.AreEqual(new string?[] { "a", null }, (string?[])result!);
    }

    [TestMethod]
    public void ThrowsWhenNullSequenceItemAndElementNotNullable()
    {
        var sequence = new InputValue.Sequence([new InputValue.Literal("a"), new InputValue.Literal(null)]);

        Assert.Throws<PipelineRunException>(
            () => InputBinder.Bind(ArrayTarget(typeof(string[])), sequence, EmptyResolver));
    }

    [TestMethod]
    public void NullValueBindsNullForNullableListParameter()
    {
        var result = InputBinder.Bind(ListTarget(typeof(IEnumerable<string>), nullable: true), new InputValue.Literal(null), EmptyResolver);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ThrowsWhenNullValueBindsToNonNullableListParameter()
    {
        Assert.Throws<PipelineRunException>(
            () => InputBinder.Bind(ListTarget(typeof(IEnumerable<string>)), new InputValue.Literal(null), EmptyResolver));
    }

    [TestMethod]
    public void PassesCollectionValuedResultThroughToMatchingParameter()
    {
        var dictionary = new Dictionary<string, string> { ["de"] = "Hallo" };

        var result = InputBinder.Bind(Single(typeof(Dictionary<string, string>)), new InputValue.StepOutputReference("s", "o"), ResolverReturning(dictionary));

        Assert.AreSame(dictionary, result);
    }

    [TestMethod]
    public void PassesMultiEntryCollectionValuedResultThroughToMatchingParameter()
    {
        var dictionary = new Dictionary<string, string> { ["de"] = "Hallo", ["en"] = "Hello" };

        var result = InputBinder.Bind(Single(typeof(Dictionary<string, string>)), new InputValue.StepOutputReference("s", "o"), ResolverReturning(dictionary));

        Assert.AreSame(dictionary, result);
    }

    [TestMethod]
    public void MissingInputForArrayParameterBindsEmptyArray()
    {
        var result = InputBinder.Bind(ArrayTarget(typeof(string[])), null, EmptyResolver);

        Assert.HasCount(0, (string[])result!);
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

    [TestMethod]
    public void FromParameterReadsEnumerableNullability()
    {
        var parameters = typeof(InputBinderTest)
            .GetMethod(nameof(SampleEnumerableParameters), BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

        var nonNullableElements = BindingTarget.FromParameter(parameters[0]);
        var nullableElements = BindingTarget.FromParameter(parameters[1]);
        var nullableEnumerable = BindingTarget.FromParameter(parameters[2]);

        Assert.IsFalse(nonNullableElements.IsElementNullable);
        Assert.IsTrue(nullableElements.IsElementNullable);
        Assert.IsTrue(nullableEnumerable.IsNullable);
    }

    [TestMethod]
    public void BindsFileReferenceToPipelineFileParameter()
    {
        var file = new Mock<IPipelineFile>().Object;

        var result = InputBinder.Bind(Single(typeof(IPipelineFile)), new InputValue.FileReference("templates/header.xtf"), ResolverReturning(file));

        Assert.AreSame(file, result);
    }

    [TestMethod]
    public void BindsSequenceOfFileReferencesToFileArray()
    {
        var first = new Mock<IPipelineFile>().Object;
        var second = new Mock<IPipelineFile>().Object;
        var files = new object?[] { first, second };
        var index = 0;
        ReferenceResolver resolver = (InputValue reference, out object? value) =>
        {
            value = files[index++];
            return true;
        };
        var sequence = new InputValue.Sequence([new InputValue.FileReference("a.xtf"), new InputValue.FileReference("b.xtf")]);

        var result = InputBinder.Bind(ArrayTarget(typeof(IPipelineFile[])), sequence, resolver);

        CollectionAssert.AreEqual(new[] { first, second }, (IPipelineFile[])result!);
    }

    [TestMethod]
    public void ThrowsWhenFileReferenceCannotBeResolved()
    {
        Assert.Throws<PipelineRunException>(
            () => InputBinder.Bind(Single(typeof(IPipelineFile)), new InputValue.FileReference("templates/header.xtf"), EmptyResolver));
    }

    [TestMethod]
    public void SpreadsUploadReferenceIntoFileArrayParameter()
    {
        var first = new Mock<IPipelineFile>().Object;
        var second = new Mock<IPipelineFile>().Object;
        var uploadList = new List<IPipelineFile> { first, second };

        var result = InputBinder.Bind(ArrayTarget(typeof(IPipelineFile[])), new InputValue.UploadReference(), ResolverReturning(uploadList));

        CollectionAssert.AreEqual(new[] { first, second }, (IPipelineFile[])result!);
    }

    private static BindingTarget Single(Type type, bool nullable = false) => new("param", type, nullable, false);

    private static BindingTarget ArrayTarget(Type type, bool elementNullable = false) => new("param", type, false, elementNullable);

    private static BindingTarget ListTarget(Type type, bool elementNullable = false, bool nullable = false) => new("param", type, nullable, elementNullable);

    private static ReferenceResolver ResolverReturning(object? value) =>
        (InputValue reference, out object? resolved) =>
        {
            resolved = value;
            return true;
        };

    private static void SampleParameters(int nonNullableInt, int? nullableInt, string?[] nullableStrings, string[] nonNullableStrings)
    {
    }

    private static void SampleEnumerableParameters(
        IEnumerable<string> nonNullableElements,
        IEnumerable<string?> nullableElements,
        IEnumerable<string>? nullableEnumerable)
    {
    }

    private sealed class ExampleComplexObject
    {
        public int Count { get; set; }
    }
}
