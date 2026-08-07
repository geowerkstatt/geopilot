using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using System.Collections;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class InputBindingValidatorTest
{
    private enum Severity
    {
        Info,
        Warning,
        Error,
    }

    [TestMethod]
    public void AcceptsInputMatchingARunParameter()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("title", "report")), null, null);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void AcceptsNullInput()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), null, null, null);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void RejectsInputKeyThatMatchesNoParameter()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("titel", "report")), null, null);

        Assert.HasCount(1, errors);
        Assert.Contains("titel", errors[0]);
    }

    [TestMethod]
    public void RejectsInputKeyTargetingCancellationToken()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("cancellationToken", "x")), null, null);

        Assert.HasCount(1, errors);
    }

    [TestMethod]
    public void RejectsLiteralThatCannotConvertToParameterType()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("maxErrors", "not a number")), null, null);

        Assert.HasCount(1, errors);
        Assert.Contains("maxErrors", errors[0]);
    }

    [TestMethod]
    public void AcceptsLiteralThatConvertsToParameterType()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("maxErrors", "42")), null, null);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void SkipsTypeCheckForStepOutputReference()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("maxErrors", "${step_output(detect.count)}")), null, null);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void AcceptsFileReferenceForFileParameterWithoutRoot()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("template", "${file(templates/header.xtf)}")), null, null);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void RejectsFileReferenceForNonFileParameter()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("title", "${file(templates/header.xtf)}")), null, null);

        Assert.HasCount(1, errors);
        Assert.Contains("title", errors[0]);
    }

    [TestMethod]
    public void RejectsFileReferenceToMissingFileUnderRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "geopilot-missing-resources");

        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("template", "${file(missing.xtf)}")), root, null);

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

            var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("template", "${file(template.xtf)}")), root, null);

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
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("files", "${upload()}")), null, null);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void RejectsUploadReferenceForNonFileListParameter()
    {
        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("title", "${upload()}")), null, null);

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

    [TestMethod]
    public void RejectsStepOutputReferenceToUnknownOutput()
    {
        var stepResultTypes = new Dictionary<string, Type> { ["up"] = typeof(UpstreamResult) };

        var errors = InputBindingValidator.Validate(
            typeof(SampleProcess),
            Input(("template", "${step_output(up.DoesNotExist)}")),
            null,
            stepResultTypes);

        Assert.HasCount(1, errors);
        Assert.Contains("DoesNotExist", errors[0]);
    }

    [TestMethod]
    public void AcceptsStepOutputReferenceToKnownOutput()
    {
        var stepResultTypes = new Dictionary<string, Type> { ["up"] = typeof(UpstreamResult) };

        var errors = InputBindingValidator.Validate(
            typeof(SampleProcess),
            Input(("template", "${step_output(up.Document)}")),
            null,
            stepResultTypes);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void RejectsStepOutputReferenceOfIncompatibleType()
    {
        var stepResultTypes = new Dictionary<string, Type> { ["up"] = typeof(UpstreamResult) };

        var errors = InputBindingValidator.Validate(typeof(SampleProcess), Input(("template", "${step_output(up.Count)}")), null, stepResultTypes);
        Assert.HasCount(1, errors);
        Assert.Contains("up.Count", errors[0]);
    }

    [TestMethod]
    public void AcceptsStepOutputReferenceOfCompatibleType()
    {
        var stepResultTypes = new Dictionary<string, Type> { ["up"] = typeof(UpstreamResult) };

        var errors = InputBindingValidator.Validate(
            typeof(SampleProcess),
            Input(("files", "${step_output(up.Documents)}")),
            null,
            stepResultTypes);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void AcceptsStepOutputReferenceThatSpreadsCollectionToSingleParameter()
    {
        var stepResultTypes = new Dictionary<string, Type> { ["up"] = typeof(UpstreamResult) };

        // up.Documents is IPipelineFile[]; the target parameter 'template' is a single IPipelineFile.
        // The binder spreads a collection onto a single parameter, so this must not be rejected at load time.
        var errors = InputBindingValidator.Validate(
            typeof(SampleProcess),
            Input(("template", "${step_output(up.Documents)}")),
            null,
            stepResultTypes);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void AcceptsStepOutputReferenceThatWrapsSingleIntoCollectionParameter()
    {
        var stepResultTypes = new Dictionary<string, Type> { ["up"] = typeof(UpstreamResult) };

        // up.Document is a single IPipelineFile; the target parameter 'files' is IPipelineFile[].
        // The binder wraps a single value into a collection, so this must not be rejected at load time.
        var errors = InputBindingValidator.Validate(
            typeof(SampleProcess),
            Input(("files", "${step_output(up.Document)}")),
            null,
            stepResultTypes);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void AcceptsStepOutputReferencesInsideAList()
    {
        var stepResultTypes = new Dictionary<string, Type> { ["up"] = typeof(UpstreamResult) };

        // The value is a YAML list; each step output reference inside it is type checked. up.Document
        // (IPipelineFile) and up.Documents (IPipelineFile[]) both bind to the IPipelineFile[] parameter.
        var errors = InputBindingValidator.Validate(
            typeof(SampleProcess),
            Input(("files", new List<object?> { "${step_output(up.Document)}", "${step_output(up.Documents)}" })),
            null,
            stepResultTypes);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void RejectsStepOutputReferenceOfIncompatibleTypeInsideAList()
    {
        var stepResultTypes = new Dictionary<string, Type> { ["up"] = typeof(UpstreamResult) };

        // A reference inside a list is not exempt from the type check: up.Count is an int, which cannot
        // bind to the IPipelineFile[] parameter 'files', so the list item is rejected.
        var errors = InputBindingValidator.Validate(
            typeof(SampleProcess),
            Input(("files", new List<object?> { "${step_output(up.Count)}" })),
            null,
            stepResultTypes);

        Assert.HasCount(1, errors);
        Assert.Contains("up.Count", errors[0]);
    }

    [TestMethod]
    public void SkipsStepOutputReferenceToUnknownStep()
    {
        var stepResultTypes = new Dictionary<string, Type> { ["up"] = typeof(UpstreamResult) };

        // The referenced step 'other' has no entry in the result-type map (an unknown step, or one whose
        // result type could not be resolved). The reference is left unchecked instead of reported as an
        // error, so a valid pipeline is never rejected because a step type was unavailable.
        var errors = InputBindingValidator.Validate(
            typeof(SampleProcess),
            Input(("template", "${step_output(other.Whatever)}")),
            null,
            stepResultTypes);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    [DataRow("maxErrors")]
    [DataRow("threshold")]
    [DataRow("strict")]
    [DataRow("timeout")]
    [DataRow("severity")]
    public void AcceptsStringOutputBoundToConvertibleParameter(string parameterName)
    {
        var stepResultTypes = new Dictionary<string, Type> { ["up"] = typeof(UpstreamResult) };

        // up.Name is a string; the binder converts a string to each of these parameter types at run time
        // via the shared conversion table, so the load-time check must accept the same string-to-X pairs.
        var errors = InputBindingValidator.Validate(
            typeof(SampleProcess),
            Input((parameterName, "${step_output(up.Name)}")),
            null,
            stepResultTypes);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void AcceptsObjectTypedOutputBoundToParameter()
    {
        var stepResultTypes = new Dictionary<string, Type> { ["up"] = typeof(UpstreamResult) };

        // up.Payload is declared as object; its run-time value may already be an IPipelineFile. The binder
        // binds an assignable value, so a base-typed source must not be rejected at load time.
        var errors = InputBindingValidator.Validate(
            typeof(SampleProcess),
            Input(("template", "${step_output(up.Payload)}")),
            null,
            stepResultTypes);

        Assert.HasCount(0, errors);
    }

    [TestMethod]
    public void AcceptsCollectionOutputBoundToConcreteListParameter()
    {
        var stepResultTypes = new Dictionary<string, Type> { ["up"] = typeof(UpstreamResult) };

        // up.Tags is a string[]; the target parameter 'tags' is a concrete List<string>. List<T> is not one
        // of the binder's element-wise list targets but a value it builds by JSON round-trip, which the
        // load-time check treats leniently, so this must not be rejected.
        var errors = InputBindingValidator.Validate(
            typeof(SampleProcess),
            Input(("tags", "${step_output(up.Tags)}")),
            null,
            stepResultTypes);

        Assert.HasCount(0, errors);
    }

    public static IEnumerable<object[]> BinderConvertibleCases =>
    [
        ["42", typeof(int)],
        ["1.5", typeof(double)],
        ["00:00:01", typeof(TimeSpan)],
        ["Warning", typeof(Severity)],
        [new[] { 1, 2 }, typeof(IEnumerable<int>)],
        [new List<string> { "a", "b" }, typeof(string[])],
        [new ArrayList { "a", "b" }, typeof(string[])],
        ["hello", typeof(string)],
    ];

    [TestMethod]
    [DynamicData(nameof(BinderConvertibleCases))]
    public void IsBindableAcceptsEveryTypePairTheBinderConverts(object value, Type targetType)
    {
        // The load-time check must never be stricter than the run-time binder: whenever the binder's leaf
        // converter (RawValueConverter) converts a value, IsBindable must accept the matching source and
        // target types. This pins the "no stricter than the binder" invariant to a test rather than a
        // comment, so a conversion added straight to TryConvert, bypassing the shared table, cannot break
        // it silently.
        if (!RawValueConverter.TryConvert(value, targetType, out _))
            return;

        Assert.IsTrue(InputBindingValidator.IsBindable(value.GetType(), targetType));
    }

    private sealed class SampleProcess
    {
        [PipelineProcessRun]
        public Task<SampleResult> RunAsync(
            string title,
            int maxErrors,
            double threshold,
            bool strict,
            TimeSpan timeout,
            Severity severity,
            List<string> tags,
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

    private sealed class UpstreamResult
    {
        public string Name { get; init; }

        public int Count { get; init; }

        public object Payload { get; init; }

        public IPipelineFile Document { get; init; }

        public IPipelineFile[] Documents { get; init; }

        public string[] Tags { get; init; }
    }
}
