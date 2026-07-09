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
    public void RejectsSequenceValueAsUnsupportedShape()
    {
        var sequence = new List<object?> { "a", "b" };

        Assert.Throws<InputCompilationException>(
            () => InputCompiler.Compile(new Dictionary<string, object?> { ["tags"] = sequence }));
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
