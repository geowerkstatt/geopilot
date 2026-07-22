using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class InputBindingValidatorTest
{
    [TestMethod]
    public void AcceptsInputMatchingARunParameter()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("title", "report")));

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void AcceptsNullInput()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), null);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void RejectsInputKeyThatMatchesNoParameter()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("titel", "report")));

        Assert.HasCount(1, errors);
        Assert.Contains("titel", errors[0]);
    }

    [TestMethod]
    public void RejectsInputKeyTargetingCancellationToken()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("cancellationToken", "x")));

        Assert.HasCount(1, errors);
    }

    [TestMethod]
    public void RejectsLiteralThatCannotConvertToParameterType()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("maxErrors", "not a number")));

        Assert.HasCount(1, errors);
        Assert.Contains("maxErrors", errors[0]);
    }

    [TestMethod]
    public void AcceptsLiteralThatConvertsToParameterType()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("maxErrors", "42")));

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void SkipsTypeCheckForStepOutputReference()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("maxErrors", "${step_output(detect.count)}")));

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void AcceptsFileReferenceForFileParameterWithoutRoot()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("template", "${file(templates/header.xtf)}")));

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void RejectsFileReferenceForNonFileParameter()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("title", "${file(templates/header.xtf)}")));

        Assert.HasCount(1, errors);
        Assert.Contains("title", errors[0]);
    }

    [TestMethod]
    public void RejectsFileReferenceToMissingFileUnderRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "geopilot-missing-resources");

        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("template", "${file(missing.xtf)}")), root);

        Assert.HasCount(1, errors);
        Assert.Contains("does not exist", errors[0]);
    }

    [TestMethod]
    public void AcceptsFileReferenceToExistingFileUnderRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "geopilot-resources-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "template.xtf"), "content");

            var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("template", "${file(template.xtf)}")), root);

            Assert.HasCount(0, errors);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void AcceptsUploadReferenceForFileListParameter()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("files", "${upload()}")));

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void RejectsUploadReferenceForNonFileListParameter()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("title", "${upload()}")));

        Assert.HasCount(1, errors);
        Assert.Contains("title", errors[0]);
    }

    private static InputConfig Input(params (string Key, object? Value)[] entries)
    {
        var input = new InputConfig();
        foreach (var (key, value) in entries)
            input[key] = value;
        return input;
    }

    private sealed class SampleProcess
    {
        [PipelineProcessRun]
        public Task<SampleResult> RunAsync(
            string title,
            int maxErrors,
            IPipelineFile template,
            IPipelineFile[] files,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new SampleResult());
        }
    }

    private sealed class SampleResult
    {
    }
}
