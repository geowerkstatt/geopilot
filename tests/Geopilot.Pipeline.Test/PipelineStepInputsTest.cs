using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;
using Moq;
using System.Globalization;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class PipelineStepInputsTest
{
    // SHA-256 of the five bytes "hello", the content of the resource file every test finds below the resources directory.
    private const string HelloSha256 = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

    private string workDirectory = string.Empty;
    private string resourcesDirectory = string.Empty;
    private string pipelineDirectory = string.Empty;

    [TestInitialize]
    public void SetUp()
    {
        // Unique per test, because the assembly runs test methods in parallel.
        workDirectory = Path.Combine(Path.GetTempPath(), $"geopilot-step-inputs-{Guid.NewGuid()}");
        resourcesDirectory = Path.Combine(workDirectory, "resources");
        pipelineDirectory = Path.Combine(workDirectory, "pipeline");
        Directory.CreateDirectory(Path.Combine(resourcesDirectory, "dmav"));
        Directory.CreateDirectory(pipelineDirectory);
        File.WriteAllText(Path.Combine(resourcesDirectory, "dmav", "mapping.xtf"), "hello");
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(workDirectory))
            Directory.Delete(workDirectory, true);
    }

    [TestMethod]
    public async Task RunReportsTheLiteralsOfASequenceWithTheirValues()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["values"] = new InputValue.Sequence([new InputValue.Literal("a"), new InputValue.Literal("b")]),
        };

        var reported = await RunAsync(inputs, Context());

        CollectionAssert.AreEqual(
            new[]
            {
                new StepInput("values", InputOrigin.Definition, null, InputKind.Value, "a"),
                new StepInput("values", InputOrigin.Definition, null, InputKind.Value, "b"),
            },
            reported);
    }

    [TestMethod]
    public async Task RunReportsEveryElementOfAStepOutputWithItsReference()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["values"] = new InputValue.StepOutputReference("extract", "Values"),
        };

        var reported = await RunAsync(inputs, Context(earlierSteps: [("extract", new EarlierOutputs { Values = new[] { "x", "y" } })]));

        CollectionAssert.AreEqual(
            new[]
            {
                new StepInput("values", InputOrigin.StepOutput, "extract.Values", InputKind.Value, "x"),
                new StepInput("values", InputOrigin.StepOutput, "extract.Values", InputKind.Value, "y"),
            },
            reported);
    }

    [TestMethod]
    public async Task RunReportsNoEntryForAnEmptyStepOutput()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["values"] = new InputValue.StepOutputReference("extract", "Values"),
        };

        var reported = await RunAsync(inputs, Context(earlierSteps: [("extract", new EarlierOutputs { Values = Array.Empty<string>() })]));

        Assert.IsNotNull(reported, "the step must report its inputs even when they are empty.");
        Assert.IsEmpty(reported);
    }

    [TestMethod]
    public async Task RunReportsAReferenceToASkippedStepAsAbsent()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["values"] = new InputValue.Sequence([new InputValue.Literal("a"), new InputValue.StepOutputReference("unzip", "Values")]),
        };

        // A skipped step is recorded without a process result.
        var reported = await RunAsync(inputs, Context(earlierSteps: [("unzip", null)]));

        CollectionAssert.AreEqual(
            new[]
            {
                new StepInput("values", InputOrigin.Definition, null, InputKind.Value, "a"),
                new StepInput("values", InputOrigin.StepOutput, "unzip.Values", InputKind.Absent),
            },
            reported);
    }

    [TestMethod]
    public async Task RunReportsANullStepOutputAsAValueWithoutText()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["scope"] = new InputValue.StepOutputReference("metadata", "Scope"),
        };

        var reported = await RunAsync(inputs, Context(earlierSteps: [("metadata", new EarlierOutputs { Scope = null })]));

        CollectionAssert.AreEqual(
            new[] { new StepInput("scope", InputOrigin.StepOutput, "metadata.Scope", InputKind.Value, null) },
            reported);
    }

    [TestMethod]
    public async Task RunReportsValuesInTheInvariantCulture()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["number"] = new InputValue.StepOutputReference("measure", "Number"),
        };

        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var reported = await RunAsync(inputs, Context(earlierSteps: [("measure", new EarlierOutputs { Number = 1.5 })]));

            CollectionAssert.AreEqual(
                new[] { new StepInput("number", InputOrigin.StepOutput, "measure.Number", InputKind.Value, "1.5") },
                reported);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public async Task RunReportsUploadedFilesWithoutReadingThem()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["files"] = new InputValue.UploadReference(),
        };

        var reported = await RunAsync(inputs, Context(upload: [UploadFile("a.xtf"), UploadFile("b.ili")]));

        CollectionAssert.AreEqual(
            new[]
            {
                new StepInput("files", InputOrigin.Upload, null, InputKind.File, FileName: "a.xtf", FromUpload: true),
                new StepInput("files", InputOrigin.Upload, null, InputKind.File, FileName: "b.ili", FromUpload: true),
            },
            reported);
    }

    [TestMethod]
    public async Task RunReportsAResourceWithItsPathSizeAndHash()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["files"] = new InputValue.FileReference("dmav/mapping.xtf"),
        };

        var reported = await RunAsync(inputs, Context());

        CollectionAssert.AreEqual(
            new[] { new StepInput("files", InputOrigin.Resource, "dmav/mapping.xtf", InputKind.File, FileName: "mapping.xtf", Size: 5, Sha256: HelloSha256, FromUpload: false) },
            reported);
    }

    [TestMethod]
    public async Task RunReportsAFileOfAnEarlierStepWithItsSizeButNoHash()
    {
        var producingStepDirectory = Directory.CreateDirectory(Path.Combine(pipelineDirectory, "convert")).FullName;
        var producedPath = Path.Combine(producingStepDirectory, "out.txt");
        File.WriteAllText(producedPath, "abc");
        var inputs = new Dictionary<string, InputValue>
        {
            ["files"] = new InputValue.StepOutputReference("convert", "Files"),
        };

        var reported = await RunAsync(inputs, Context(earlierSteps: [("convert", new EarlierOutputs { Files = new IPipelineFile[] { new PipelineFile(producedPath, "out.txt") } })]));

        CollectionAssert.AreEqual(
            new[] { new StepInput("files", InputOrigin.StepOutput, "convert.Files", InputKind.File, FileName: "out.txt", Size: 3, FromUpload: false) },
            reported);
    }

    [TestMethod]
    public async Task RunReportsAnUploadAnEarlierStepPassedOnAsFromUpload()
    {
        var upload = UploadFile("a.xtf");

        // A matcher passes on the view of the upload it was given, not the upload itself.
        var passedOn = new CopyOnWriteFile(upload, pipelineDirectory, "matcher");
        var inputs = new Dictionary<string, InputValue>
        {
            ["files"] = new InputValue.StepOutputReference("matcher", "Files"),
        };

        var reported = await RunAsync(inputs, Context(upload: [upload], earlierSteps: [("matcher", new EarlierOutputs { Files = new IPipelineFile[] { passedOn } })]));

        CollectionAssert.AreEqual(
            new[] { new StepInput("files", InputOrigin.StepOutput, "matcher.Files", InputKind.File, FileName: "a.xtf", FromUpload: true) },
            reported);
    }

    [TestMethod]
    public async Task RunReportsTheConfiguredFilesAfterTheRunInputs()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["values"] = new InputValue.Literal("a"),
        };
        var mapping = new PipelineFile(Path.Combine(resourcesDirectory, "dmav", "mapping.xtf"), "mapping.xtf");

        var reported = await RunAsync(inputs, Context(), configuredFiles: [new ConfiguredFile("refMappingFile", "dmav/mapping.xtf", mapping)]);

        CollectionAssert.AreEqual(
            new[]
            {
                new StepInput("values", InputOrigin.Definition, null, InputKind.Value, "a"),
                new StepInput("refMappingFile", InputOrigin.Configuration, "dmav/mapping.xtf", InputKind.File, FileName: "mapping.xtf", Size: 5, Sha256: HelloSha256, FromUpload: false),
            },
            reported);
    }

    [TestMethod]
    public async Task RunReportsAFileItCannotReadWithoutSizeAndHash()
    {
        // Gone since the process was built: the protocol must not fail the step, the process reports its own error.
        var missing = new PipelineFile(Path.Combine(resourcesDirectory, "dmav", "removed.xtf"), "removed.xtf");

        var reported = await RunAsync(new Dictionary<string, InputValue>(), Context(), configuredFiles: [new ConfiguredFile("refMappingFile", "dmav/removed.xtf", missing)]);

        CollectionAssert.AreEqual(
            new[] { new StepInput("refMappingFile", InputOrigin.Configuration, "dmav/removed.xtf", InputKind.File, FileName: "removed.xtf", FromUpload: false) },
            reported);
    }

    [TestMethod]
    public async Task RunReportsTheInputsBeforeTheProcessRuns()
    {
        var events = new List<string>();
        var context = new PipelineContext
        {
            Upload = [],
            StepResults = [],
            OnStepInputsBound = (step, stepInputs, cancellationToken) =>
            {
                events.Add("inputs");
                return Task.CompletedTask;
            },
        };
        using var step = BuildStep(new Dictionary<string, InputValue>(), new InputsProcess(events));

        await step.Run(context, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "inputs", "process" }, events);
    }

    [TestMethod]
    public async Task RunReportsNoInputsForASkippedStep()
    {
        var reported = false;
        var context = new PipelineContext
        {
            Upload = [],
            StepResults = [],
            OnStepInputsBound = (step, stepInputs, cancellationToken) =>
            {
                reported = true;
                return Task.CompletedTask;
            },
        };
        var skipAlways = new PipelineStepConditionsConfig
        {
            Pre = new PipelineStepPreConditionConfig { SkipConditions = [new ConditionConfig { Expression = "1 == 1" }] },
        };
        using var step = BuildStep(new Dictionary<string, InputValue> { ["values"] = new InputValue.Literal("a") }, new InputsProcess([]), stepConditions: skipAlways);

        await step.Run(context, CancellationToken.None);

        Assert.AreEqual(StepState.Skipped, step.State);
        Assert.IsFalse(reported, "a skipped step binds nothing, so there is nothing to report.");
    }

    private async Task<StepInput[]?> RunAsync(Dictionary<string, InputValue> inputs, Func<Action<IReadOnlyList<StepInput>>, PipelineContext> context, IReadOnlyList<ConfiguredFile>? configuredFiles = null)
    {
        IReadOnlyList<StepInput>? reported = null;
        using var step = BuildStep(inputs, new InputsProcess([]), configuredFiles);

        await step.Run(context(stepInputs => reported = stepInputs), CancellationToken.None);

        Assert.AreEqual(StepState.Success, step.State, step.ErrorMessage);
        return reported?.ToArray();
    }

    private static Func<Action<IReadOnlyList<StepInput>>, PipelineContext> Context(IReadOnlyList<IPipelineFile>? upload = null, (string StepId, object? Result)[]? earlierSteps = null) =>
        report => new PipelineContext
        {
            Upload = upload ?? [],
            StepResults = (earlierSteps ?? []).ToDictionary(step => step.StepId, step => new StepResult { Result = step.Result }),
            OnStepInputsBound = (step, stepInputs, cancellationToken) =>
            {
                report(stepInputs);
                return Task.CompletedTask;
            },
        };

    private PipelineStep BuildStep(Dictionary<string, InputValue> inputs, InputsProcess process, IReadOnlyList<ConfiguredFile>? configuredFiles = null, PipelineStepConditionsConfig? stepConditions = null) =>
        PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(LocalizedText.Empty)
            .Inputs(inputs)
            .OutputActions([])
            .StepConditions(stepConditions)
            .Process(process)
            .ConfiguredFiles(configuredFiles ?? [])
            .PipelineDirectory(pipelineDirectory)
            .ResourcesDirectory(resourcesDirectory)
            .Logger(new Mock<ILogger>().Object)
            .Build();

    // Strict, so reading the content, which would fetch an upload from its storage, fails the test.
    private static IPipelineFile UploadFile(string name)
    {
        var file = new Mock<IPipelineFile>(MockBehavior.Strict);
        file.SetupGet(f => f.OriginalFileName).Returns(name);
        return file.Object;
    }

    private sealed class InputsProcess(List<string> events)
    {
        [PipelineProcessRun]
        public Task<InputsResult> RunAsync(string[] values, IPipelineFile[] files, string? scope, double? number, CancellationToken cancellationToken)
        {
            events.Add("process");
            return Task.FromResult(new InputsResult());
        }
    }

    private sealed class InputsResult
    {
        public string? Unused { get; init; }
    }

    // The outputs of an earlier step, which the inputs of the step under test reference.
    private sealed class EarlierOutputs
    {
        public object? Values { get; init; }

        public object? Files { get; init; }

        public string? Scope { get; init; }

        public double? Number { get; init; }
    }
}
