using Geopilot.Pipeline.Config;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class InputCompilerTest
{
    [TestMethod]
    public void CompilesLiteralString()
    {
        var compiled = InputCompiler.Compile(new Dictionary<string, object?> { ["title"] = "Validation report" });

        Assert.AreEqual(new InputValue.Literal("Validation report"), compiled["title"]);
    }

    [TestMethod]
    public void KeepsNumericLiteralAsTextWithoutConverting()
    {
        var compiled = InputCompiler.Compile(new Dictionary<string, object?> { ["maxErrors"] = "100" });

        Assert.AreEqual(new InputValue.Literal("100"), compiled["maxErrors"]);
    }

    [TestMethod]
    public void CompilesExplicitNullToNullLiteral()
    {
        var compiled = InputCompiler.Compile(new Dictionary<string, object?> { ["title"] = null });

        Assert.AreEqual(new InputValue.Literal(null), compiled["title"]);
    }

    [TestMethod]
    public void CompilesStepOutputReference()
    {
        var compiled = InputCompiler.Compile(new Dictionary<string, object?> { ["firstMessage"] = "${step_output(validation.log)}" });

        Assert.AreEqual(new InputValue.StepOutputReference("validation", "log"), compiled["firstMessage"]);
    }

    [TestMethod]
    [DataRow("prefix ${step_output(a.b)}")]
    [DataRow("${step_output(a.b)} suffix")]
    public void RejectsReferenceMarkerEmbeddedInText(string value)
    {
        Assert.Throws<InputCompilationException>(
            () => InputCompiler.Compile(new Dictionary<string, object?> { ["x"] = value }));
    }

    [TestMethod]
    [DataRow("${unsupported}")]
    [DataRow("${unsupported:value}")]
    [DataRow("${uploads}")]
    [DataRow("${file(/templates/header.xtf)}")]
    public void RejectsUnsupportedReferenceKind(string reference)
    {
        var exception = Assert.Throws<InputCompilationException>(
            () => InputCompiler.Compile(new Dictionary<string, object?> { ["x"] = reference }));

        Assert.Contains("not supported", exception.Message);
    }

    [TestMethod]
    [DataRow("${step_output(bad)}")]
    [DataRow("${step_output(a.b.c)}")]
    [DataRow("${step_output(.log)}")]
    [DataRow("${step_output(validation.)}")]
    public void RejectsMalformedStepOutputReference(string reference)
    {
        Assert.Throws<InputCompilationException>(
            () => InputCompiler.Compile(new Dictionary<string, object?> { ["x"] = reference }));
    }

    [TestMethod]
    public void CompilesSequenceOfLiterals()
    {
        var compiled = InputCompiler.Compile(new Dictionary<string, object?> { ["tags"] = new List<object?> { "xtf", "ili" } });

        var sequence = (InputValue.Sequence)compiled["tags"];
        CollectionAssert.AreEqual(
            new InputValue[] { new InputValue.Literal("xtf"), new InputValue.Literal("ili") },
            sequence.Items.ToList());
    }

    [TestMethod]
    public void CompilesSequenceMixingLiteralsAndReferences()
    {
        var rawSequence = new List<object?> { "start", "${step_output(validation.log)}", "done" };

        var compiled = InputCompiler.Compile(new Dictionary<string, object?> { ["allMessages"] = rawSequence });

        var sequence = (InputValue.Sequence)compiled["allMessages"];
        CollectionAssert.AreEqual(
            new InputValue[]
            {
                new InputValue.Literal("start"),
                new InputValue.StepOutputReference("validation", "log"),
                new InputValue.Literal("done"),
            },
            sequence.Items.ToList());
    }

    [TestMethod]
    public void RejectsNestedSequence()
    {
        var nested = new List<object?> { new List<object?> { "a", "b" } };

        var exception = Assert.Throws<InputCompilationException>(
            () => InputCompiler.Compile(new Dictionary<string, object?> { ["presetMatrix"] = nested }));

        Assert.Contains("Nested lists are not supported", exception.Message);
    }

    [TestMethod]
    public void RejectsMappingInsideSequence()
    {
        var sequenceWithMapping = new List<object?> { "ok", new Dictionary<object, object?> { ["count"] = "5" } };

        Assert.Throws<InputCompilationException>(
            () => InputCompiler.Compile(new Dictionary<string, object?> { ["mixed"] = sequenceWithMapping }));
    }

    [TestMethod]
    public void RejectsMappingValueAsUnsupportedShape()
    {
        var mapping = new Dictionary<object, object?> { ["count"] = "5" };

        Assert.Throws<InputCompilationException>(
            () => InputCompiler.Compile(new Dictionary<string, object?> { ["stats"] = mapping }));
    }

    [TestMethod]
    public void CompilesEachEntryOfTheMap()
    {
        var compiled = InputCompiler.Compile(new Dictionary<string, object?>
        {
            ["title"] = "report",
            ["log"] = "${step_output(validation.log)}",
        });

        Assert.HasCount(2, compiled);
        Assert.AreEqual(new InputValue.Literal("report"), compiled["title"]);
        Assert.AreEqual(new InputValue.StepOutputReference("validation", "log"), compiled["log"]);
    }
}
