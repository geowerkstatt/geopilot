using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Process;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class PipelineStepTest
{
    private Mock<ILoggerFactory> loggerFactoryMock;
    private Mock<ILogger<PipelineProcessFactory>> loggerMock;

    [TestInitialize]
    public void SetUp()
    {
        loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerMock = new Mock<ILogger<PipelineProcessFactory>>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);
    }

    private class MockPipelineProcessSingleInput
    {
        public MockPipelineProcessSingleInput(MockPipelineProcessSingleInputResult outputData)
        {
            this.outputData = outputData;
        }

        private MockPipelineProcessSingleInputResult outputData;

        public int NumberOfRunInvoced { get; set; }

        [PipelineProcessRun]
        public async Task<MockPipelineProcessSingleInputResult> RunAsync(string data, CancellationToken cancellationToken)
        {
            Assert.IsNotNull(data);
            Assert.IsNotNull(cancellationToken);
            NumberOfRunInvoced++;
            return this.outputData;
        }
    }

    private class MockPipelineProcessSingleInputResult
    {
        public object? OutputData { get; init; }
    }

    private class MockPipelineProcessArrayInput
    {
        public MockPipelineProcessArrayInput(MockPipelineProcessArrayInputResult outputData, int expectedNumberOfInputData)
        {
            this.outputData = outputData;
            this.expectedNumberOfInputData = expectedNumberOfInputData;
        }

        private MockPipelineProcessArrayInputResult outputData;
        private int expectedNumberOfInputData;

        public int NumberOfRunInvoced { get; set; }

        [PipelineProcessRun]
        public async Task<MockPipelineProcessArrayInputResult> RunAsync(string[] data, CancellationToken cancellationToken)
        {
            Assert.IsNotNull(data);
            Assert.HasCount(expectedNumberOfInputData, data, "not matching expected count for input data");
            Assert.IsNotNull(cancellationToken);
            NumberOfRunInvoced++;
            return this.outputData;
        }
    }

    private class MockPipelineProcessArrayInputResult
    {
        public object OutputData { get; init; }
    }

    private class MockPipelineProcessOptionalSingleInput
    {
        public int NumberOfRunInvokations { get; set; }

        public string? NullableData { get; set; }

        [PipelineProcessRun]
        public async Task<Dictionary<string, object>> RunAsync(string? nullableData)
        {
            NullableData = nullableData;
            NumberOfRunInvokations++;
            return [];
        }
    }

    private class MockPipelineProcessException
    {
        public int NumberOfRunInvoced { get; set; }

        [PipelineProcessRun]
        public async Task<Dictionary<string, object>> RunAsync(string data)
        {
            ++NumberOfRunInvoced;
            throw new InvalidOperationException("Test exception during process run.");
        }
    }

    // Result stand-in for a previous step referenced by condition expressions. Its public
    // properties are the implicit outputs that expressions like [aPreviousStep.SomeRandomData] read.
    private sealed class ConditionStepResult
    {
        public int SomeRandomData { get; init; }

        public string? AnotherValue { get; init; }
    }

    private sealed class MockMultiOutputResult
    {
        public object? FirstFile { get; init; }

        public object? SecondFile { get; init; }

        public LocalizedText? Status { get; init; }
    }

    private sealed class MockMultiOutputProcess
    {
        private readonly MockMultiOutputResult result;

        public MockMultiOutputProcess(MockMultiOutputResult result) => this.result = result;

        [PipelineProcessRun]
        public Task<MockMultiOutputResult> RunAsync() => Task.FromResult(this.result);
    }

    [TestMethod]
    public async Task SuccessfullStepRunWithSingleInput()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["data"] = new InputValue.StepOutputReference("upload", "OutputData"),
        };
        var outputActions = new List<OutputActionConfig>
        {
            new OutputActionConfig
            {
                Property = "OutputData",
                Actions = new HashSet<OutputAction> { OutputAction.Download, OutputAction.Delivery },
            },
        };
        var pipelineContext = ContextWith(("upload", new MockPipelineProcessArrayInputResult { OutputData = "input_from_upload" }));
        var processData = new MockPipelineProcessSingleInputResult { OutputData = "some_data" };

        var processMock = new MockPipelineProcessSingleInput(processData);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .Inputs(inputs)
            .OutputActions(outputActions)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        Assert.AreEqual(StepState.Pending, pipelineStep.State);

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

        Assert.IsNotNull(stepResult);
        Assert.AreEqual(StepState.Success, pipelineStep.State);
        Assert.AreEqual(1, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");

        Assert.HasCount(1, stepResult.ActionOutputs);
        Assert.IsTrue(stepResult.ActionOutputs.ContainsKey("OutputData"));
        Assert.IsTrue(stepResult.ActionOutputs["OutputData"].Actions.SetEquals(new HashSet<OutputAction> { OutputAction.Download, OutputAction.Delivery }));
        Assert.AreEqual("some_data", stepResult.ActionOutputs["OutputData"].Data);
    }

    [TestMethod]
    public async Task SuccessfullStepRunWithArrayInputFromSingleArrayOutput()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["data"] = new InputValue.StepOutputReference("step_01", "OutputData"),
        };
        var pipelineContext = ContextWith(("step_01", new MockPipelineProcessArrayInputResult { OutputData = new string[] { "data 01", "data 02" } }));
        var processData = new MockPipelineProcessArrayInputResult { OutputData = "produced_output" };

        var processMock = new MockPipelineProcessArrayInput(processData, 2);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .Inputs(inputs)
            .OutputActions([])
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(StepState.Success, pipelineStep.State);
        Assert.AreEqual(1, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");

        Assert.IsEmpty(stepResult.ActionOutputs);
        Assert.IsTrue(stepResult.TryGetOutput("OutputData", out var outputData));
        Assert.AreEqual("produced_output", outputData);
    }

    [TestMethod]
    public async Task SuccessfullStepRunWithSequenceInputSpreadingAReferencedList()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["data"] = new InputValue.Sequence(
            [
                new InputValue.Literal("first"),
                new InputValue.StepOutputReference("step_01", "OutputData"),
                new InputValue.Literal("last"),
            ]),
        };
        var pipelineContext = ContextWith(("step_01", new MockPipelineProcessArrayInputResult { OutputData = new string[] { "middle_a", "middle_b" } }));
        var processData = new MockPipelineProcessArrayInputResult { OutputData = "some_data" };

        // Two literals plus the two elements of the referenced list, spread one level.
        var processMock = new MockPipelineProcessArrayInput(processData, 4);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(LocalizedText.Empty)
            .Inputs(inputs)
            .OutputActions([])
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(StepState.Success, pipelineStep.State);
        Assert.AreEqual(1, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");
        Assert.IsTrue(stepResult.TryGetOutput("OutputData", out var outputData));
        Assert.AreEqual("some_data", outputData);
    }

    [TestMethod]
    public async Task StepRunFailsWhenInputReferencesUnknownStep()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["data"] = new InputValue.StepOutputReference("invalid_upload_reference", "some_data"),
        };
        var pipelineContext = ContextWith(("upload", new MockPipelineProcessArrayInputResult { OutputData = "some_data" }));

        var processMock = new MockPipelineProcessSingleInput(new MockPipelineProcessSingleInputResult());

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .Inputs(inputs)
            .OutputActions([])
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipelineStep.Run(pipelineContext, CancellationToken.None));

        Assert.Contains("is not an output of an earlier step", exception.Message);
        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvoced, "Process Run method was invoked.");
    }

    [TestMethod]
    public async Task StepRunFailsWhenInputReferencesUnknownOutput()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["data"] = new InputValue.StepOutputReference("upload", "xtf_file_wrong_reference"),
        };
        var pipelineContext = ContextWith(("upload", new MockPipelineProcessArrayInputResult { OutputData = "some_data" }));

        var processMock = new MockPipelineProcessSingleInput(new MockPipelineProcessSingleInputResult());

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .Inputs(inputs)
            .OutputActions([])
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipelineStep.Run(pipelineContext, CancellationToken.None));

        Assert.Contains("is not an output of an earlier step", exception.Message);
        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvoced, "Process Run method was invoked.");
    }

    [TestMethod]
    public async Task SuccessfullStepRunWithNullableSingleParameterFromEmptyArrayOutput()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["nullableData"] = new InputValue.StepOutputReference("step_01", "OutputData"),
        };

        var pipelineContext = ContextWith(("step_01", new MockPipelineProcessArrayInputResult { OutputData = Array.Empty<string>() }));

        var processMock = new MockPipelineProcessOptionalSingleInput();

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(LocalizedText.Empty)
            .Inputs(inputs)
            .OutputActions([])
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(StepState.Success, pipelineStep.State);
        Assert.AreEqual(1, processMock.NumberOfRunInvokations, "Process Run method was not invoked exactly once.");
        Assert.IsNull(processMock.NullableData);
    }

    [TestMethod]
    public async Task ExceptionDuringProcessRun()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["data"] = new InputValue.StepOutputReference("upload", "OutputData"),
        };
        var pipelineContext = ContextWith(("upload", new MockPipelineProcessSingleInputResult { OutputData = "some_data" }));

        var processMock = new MockPipelineProcessException();

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .Inputs(inputs)
            .OutputActions([])
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipelineStep.Run(pipelineContext, CancellationToken.None));

        Assert.AreEqual("The process <MockPipelineProcessException> threw an exception.", exception.Message);
        Assert.AreEqual(typeof(InvalidOperationException), exception.InnerException?.GetType());
        Assert.AreEqual("Test exception during process run.", exception.InnerException?.Message);
        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(1, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");
    }

    [TestMethod]
    public async Task StepResultCouldNotBeCreated()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["data"] = new InputValue.StepOutputReference("upload", "OutputData"),
        };
        var outputActions = new List<OutputActionConfig>
        {
            new OutputActionConfig
            {
                Property = "DoesNotExistOnResult",
                Actions = new HashSet<OutputAction> { OutputAction.Download },
            },
        };
        var pipelineContext = ContextWith(("upload", new MockPipelineProcessSingleInputResult { OutputData = "some_data" }));
        var processData = new MockPipelineProcessSingleInputResult { OutputData = "some_data" };

        var processMock = new MockPipelineProcessSingleInput(processData);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .Inputs(inputs)
            .OutputActions(outputActions)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipelineStep.Run(pipelineContext, CancellationToken.None));

        Assert.Contains("is not a readable property of the result", exception.Message);
        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(1, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");
    }

    [TestMethod]
    public async Task StepRunFailsWhenVisualizationOutputIsNotAnEnvelope()
    {
        var inputs = new Dictionary<string, InputValue>
        {
            ["data"] = new InputValue.StepOutputReference("upload", "OutputData"),
        };
        var outputActions = new List<OutputActionConfig>
        {
            new OutputActionConfig
            {
                Property = "OutputData",
                Actions = new HashSet<OutputAction> { OutputAction.Visualization },
            },
        };
        var pipelineContext = ContextWith(("upload", new MockPipelineProcessSingleInputResult { OutputData = "some_data" }));
        var processData = new MockPipelineProcessSingleInputResult { OutputData = "some_data" };

        var processMock = new MockPipelineProcessSingleInput(processData);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(LocalizedText.Empty)
            .Inputs(inputs)
            .OutputActions(outputActions)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        var exception = await Assert.ThrowsAsync<PipelineRunException>(() => pipelineStep.Run(pipelineContext, CancellationToken.None));

        Assert.Contains("visualization", exception.Message);
        Assert.AreEqual(StepState.Error, pipelineStep.State);
    }

    [TestMethod]
    public async Task StepShouldFailBecauseOfPreCondition()
    {
        var inputs = SingleUploadInput();
        var pipelineContext = ContextWith(
            ("aPreviousStep", new ConditionStepResult { SomeRandomData = 123 }));
        var processData = new MockPipelineProcessSingleInputResult { OutputData = "some_data" };
        var stepConditions = new PipelineStepConditionsConfig
        {
            Pre = new PipelineStepPreConditionConfig()
            {
                SkipConditions = new List<ConditionConfig>
                {
                    new ConditionConfig { Expression = "[aPreviousStep.SomeRandomData] == 123" },
                },
                FailConditions = new List<ConditionConfig>
                {
                    new ConditionConfig
                    {
                        Expression = "[aPreviousStep.SomeRandomData] == 123",
                        Message = new Dictionary<string, string>
                        {
                            { "de", "Schritt fehlgeschlagen." },
                            { "en", "Step failed." },
                        },
                    },
                },
            },
            Post = null,
        };

        var processMock = new MockPipelineProcessSingleInput(processData);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .Inputs(inputs)
            .OutputActions([])
            .StepConditions(stepConditions)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvoced, "Process Run method was invoked but should be skipped.");

        Assert.IsTrue(stepResult.ActionOutputs.ContainsKey("my_step_status_message_pre_fail_condition"), "StepResult should contain a status_message output.");
        var statusOutput = stepResult.ActionOutputs["my_step_status_message_pre_fail_condition"];
        Assert.IsTrue(statusOutput.Actions != null && statusOutput.Actions.Contains(OutputAction.StatusMessage));
        var message = statusOutput.Data as LocalizedText;
        Assert.IsNotNull(message);
        Assert.AreEqual("Step failed.", message["en"]);
        Assert.AreEqual("Schritt fehlgeschlagen.", message["de"]);
    }

    [TestMethod]
    public async Task StepShouldBeSkipedBecauseOfPreCondition()
    {
        var inputs = SingleUploadInput();
        var pipelineContext = ContextWith(
            ("aPreviousStep", new ConditionStepResult { SomeRandomData = 123 }));
        var processData = new MockPipelineProcessSingleInputResult { OutputData = "some_data" };
        var stepConditions = new PipelineStepConditionsConfig
        {
            Pre = new PipelineStepPreConditionConfig()
            {
                SkipConditions = new List<ConditionConfig>
                {
                    new ConditionConfig
                    {
                        Expression = "[aPreviousStep.SomeRandomData] == 123",
                        Message = new Dictionary<string, string>
                        {
                            { "de", "Schritt übersprungen." },
                            { "en", "Step skipped." },
                        },
                    },
                },
                FailConditions = null,
            },
            Post = null,
        };

        var processMock = new MockPipelineProcessSingleInput(processData);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .Inputs(inputs)
            .OutputActions([])
            .StepConditions(stepConditions)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(StepState.Skipped, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvoced, "Process Run method was invoked but should be skipped.");

        Assert.IsTrue(stepResult.ActionOutputs.ContainsKey("my_step_status_message_pre_skip_condition"), "StepResult should contain a status_message output.");
        var statusOutput = stepResult.ActionOutputs["my_step_status_message_pre_skip_condition"];
        Assert.IsTrue(statusOutput.Actions != null && statusOutput.Actions.Contains(OutputAction.StatusMessage));
        var message = statusOutput.Data as LocalizedText;
        Assert.IsNotNull(message);
        Assert.AreEqual("Step skipped.", message["en"]);
        Assert.AreEqual("Schritt übersprungen.", message["de"]);
    }

    [TestMethod]
    public async Task StepShouldFailBecauseOfPostCondition()
    {
        var inputs = SingleUploadInput();
        var pipelineContext = ContextWith(
            ("upload", new MockPipelineProcessSingleInputResult { OutputData = "some_data" }),
            ("aPreviousStep", new ConditionStepResult { SomeRandomData = 123 }));
        var processData = new MockPipelineProcessSingleInputResult { OutputData = "some_data" };
        var stepConditions = new PipelineStepConditionsConfig
        {
            Pre = new PipelineStepPreConditionConfig()
            {
                SkipConditions = new List<ConditionConfig>
                {
                    new ConditionConfig { Expression = "[aPreviousStep.SomeRandomData] == 124" },
                },
                FailConditions = new List<ConditionConfig>
                {
                    new ConditionConfig { Expression = "[aPreviousStep.SomeRandomData] == 124" },
                },
            },
            Post = new PipelineStepPostConditionConfig()
            {
                FailConditions = new List<ConditionConfig>
                {
                    new ConditionConfig
                    {
                        Expression = "[my_step.OutputData] == 'some_data'",
                        Message = new Dictionary<string, string>
                        {
                            { "de", "Post-Bedingung fehlgeschlagen." },
                            { "en", "Post-condition failed." },
                        },
                    },
                },
            },
        };

        var processMock = new MockPipelineProcessSingleInput(processData);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .Inputs(inputs)
            .OutputActions([])
            .StepConditions(stepConditions)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(1, processMock.NumberOfRunInvoced, "Process Run method was not invoked exactly once.");

        Assert.IsTrue(stepResult.ActionOutputs.ContainsKey("my_step_status_message_post_fail_condition"), "StepResult should contain a status_message output.");
        var statusOutput = stepResult.ActionOutputs["my_step_status_message_post_fail_condition"];
        Assert.IsTrue(statusOutput.Actions != null && statusOutput.Actions.Contains(OutputAction.StatusMessage));
        var message = statusOutput.Data as LocalizedText;
        Assert.IsNotNull(message);
        Assert.AreEqual("Post-condition failed.", message["en"]);
        Assert.AreEqual("Post-Bedingung fehlgeschlagen.", message["de"]);
    }

    [TestMethod]
    public async Task StepShouldFailWithMultiplePreFailConditionsAndConcatenatedMessages()
    {
        var inputs = SingleUploadInput();
        var pipelineContext = ContextWith(
            ("aPreviousStep", new ConditionStepResult { SomeRandomData = 123, AnotherValue = "abc" }));
        var processData = new MockPipelineProcessSingleInputResult { OutputData = "some_data" };
        var stepConditions = new PipelineStepConditionsConfig
        {
            Pre = new PipelineStepPreConditionConfig()
            {
                SkipConditions = null,
                FailConditions = new List<ConditionConfig>
                {
                    new ConditionConfig
                    {
                        Expression = "[aPreviousStep.SomeRandomData] == 123",
                        Message = new Dictionary<string, string>
                        {
                            { "de", "Erste Bedingung fehlgeschlagen" },
                            { "en", "First condition failed" },
                        },
                    },
                    new ConditionConfig
                    {
                        Expression = "[aPreviousStep.AnotherValue] == 'abc'",
                        Message = new Dictionary<string, string>
                        {
                            { "de", "Zweite Bedingung fehlgeschlagen" },
                            { "en", "Second condition failed" },
                            { "fr", "Deuxième condition échouée" },
                        },
                    },
                    new ConditionConfig
                    {
                        Expression = "[aPreviousStep.SomeRandomData] == 999",
                        Message = new Dictionary<string, string>
                        {
                            { "de", "Dritte Bedingung fehlgeschlagen" },
                            { "en", "Third condition failed" },
                        },
                    },
                },
            },
            Post = null,
        };

        var processMock = new MockPipelineProcessSingleInput(processData);

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(new Dictionary<string, string>() { { "de", "my step" } })
            .Inputs(inputs)
            .OutputActions([])
            .StepConditions(stepConditions)
            .Process(processMock)
            .Logger(loggerMock.Object)
            .Build();

        var stepResult = await pipelineStep.Run(pipelineContext, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(StepState.Error, pipelineStep.State);
        Assert.AreEqual(0, processMock.NumberOfRunInvoced, "Process Run method was invoked but should be skipped.");

        Assert.IsTrue(stepResult.ActionOutputs.ContainsKey("my_step_status_message_pre_fail_condition"), "StepResult should contain a status_message output.");
        var statusOutput = stepResult.ActionOutputs["my_step_status_message_pre_fail_condition"];
        Assert.IsTrue(statusOutput.Actions != null && statusOutput.Actions.Contains(OutputAction.StatusMessage));
        var message = statusOutput.Data as LocalizedText;
        Assert.IsNotNull(message);

        // First and second conditions match, third does not (999 != 123).
        // Messages are concatenated comma-separated per language.
        Assert.AreEqual("First condition failed, Second condition failed", message["en"]);
        Assert.AreEqual("Erste Bedingung fehlgeschlagen, Zweite Bedingung fehlgeschlagen", message["de"]);

        // French only present on the second condition.
        Assert.AreEqual("Deuxième condition échouée", message["fr"]);
    }

    [TestMethod]
    public void DownloadsReturnsImmutableSnapshotNotAffectedByLaterAdds()
    {
        using var step = BuildBareStep();

        step.AddDownload(new PersistedFile("a.txt", "my_step_a.txt"));
        var snapshot = step.Downloads;
        step.AddDownload(new PersistedFile("b.txt", "my_step_b.txt"));

        Assert.HasCount(1, snapshot, "A previously read reference must not observe later additions.");
        Assert.HasCount(2, step.Downloads, "A fresh read must observe all additions.");
    }

    [TestMethod]
    public void DeliveryFilesReturnsImmutableSnapshotNotAffectedByLaterAdds()
    {
        using var step = BuildBareStep();

        step.AddDeliveryFile(new PersistedFile("a.txt", "my_step_a.txt"));
        var snapshot = step.DeliveryFiles;
        step.AddDeliveryFile(new PersistedFile("b.txt", "my_step_b.txt"));

        Assert.HasCount(1, snapshot, "A previously read reference must not observe later additions.");
        Assert.HasCount(2, step.DeliveryFiles, "A fresh read must observe all additions.");
    }

    [TestMethod]
    public void AddDownloadPreservesInsertionOrder()
    {
        using var step = BuildBareStep();
        var a = new PersistedFile("a.txt", "my_step_a.txt");
        var b = new PersistedFile("b.txt", "my_step_b.txt");
        var c = new PersistedFile("c.txt", "my_step_c.txt");

        step.AddDownload(a);
        step.AddDownload(b);
        step.AddDownload(c);

        CollectionAssert.AreEqual(new[] { a, b, c }, step.Downloads.ToList());
    }

    [TestMethod]
    public async Task AddDownloadIsThreadSafeWhileEnumerating()
    {
        using var step = BuildBareStep();
        const int count = 1000;

        var enumerate = Task.Run(() =>
        {
            for (var i = 0; i < count; i++)
            {
                // Enumerate concurrently with additions; this must never throw.
                foreach (var file in step.Downloads)
                    Assert.IsNotNull(file);
            }
        });

        var add = Task.Run(() =>
        {
            for (var i = 0; i < count; i++)
                step.AddDownload(new PersistedFile($"f{i}.txt", $"my_step_f{i}.txt"));
        });

        await Task.WhenAll(enumerate, add);

        Assert.HasCount(count, step.Downloads);
    }

    [TestMethod]
    public async Task StepRunAppliesActionsToMultipleOutputs()
    {
        var result = new MockMultiOutputResult
        {
            FirstFile = "first",
            SecondFile = "second",
            Status = new Dictionary<string, string> { ["en"] = "done" },
        };
        var outputActions = new List<OutputActionConfig>
        {
            new OutputActionConfig { Property = "FirstFile", Actions = new HashSet<OutputAction> { OutputAction.Download } },
            new OutputActionConfig { Property = "SecondFile", Actions = new HashSet<OutputAction> { OutputAction.Download, OutputAction.Delivery } },
            new OutputActionConfig { Property = "Status", Actions = new HashSet<OutputAction> { OutputAction.StatusMessage } },
        };

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(LocalizedText.Empty)
            .Inputs(new Dictionary<string, InputValue>())
            .OutputActions(outputActions)
            .Process(new MockMultiOutputProcess(result))
            .Logger(loggerMock.Object)
            .Build();

        var stepResult = await pipelineStep.Run(ContextWith(), CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(StepState.Success, pipelineStep.State);
        Assert.HasCount(3, stepResult.ActionOutputs);

        Assert.AreEqual("first", stepResult.ActionOutputs["FirstFile"].Data);
        Assert.IsTrue(stepResult.ActionOutputs["FirstFile"].Actions.SetEquals(new HashSet<OutputAction> { OutputAction.Download }));

        Assert.AreEqual("second", stepResult.ActionOutputs["SecondFile"].Data);
        Assert.IsTrue(stepResult.ActionOutputs["SecondFile"].Actions.SetEquals(new HashSet<OutputAction> { OutputAction.Download, OutputAction.Delivery }));

        Assert.IsTrue(stepResult.ActionOutputs["Status"].Actions.SetEquals(new HashSet<OutputAction> { OutputAction.StatusMessage }));
    }

    [TestMethod]
    public async Task StepStatusMessageIsExtractedFromOutputTaggedStatusMessage()
    {
        var result = new MockMultiOutputResult
        {
            Status = new Dictionary<string, string> { ["de"] = "Fertig.", ["en"] = "Done." },
        };
        var outputActions = new List<OutputActionConfig>
        {
            new OutputActionConfig { Property = "Status", Actions = new HashSet<OutputAction> { OutputAction.StatusMessage } },
        };

        using var pipelineStep = PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(LocalizedText.Empty)
            .Inputs(new Dictionary<string, InputValue>())
            .OutputActions(outputActions)
            .Process(new MockMultiOutputProcess(result))
            .Logger(loggerMock.Object)
            .Build();

        await pipelineStep.Run(ContextWith(), CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(StepState.Success, pipelineStep.State);

        var message = pipelineStep.StatusMessage;
        Assert.IsNotNull(message);
        Assert.AreEqual("Done.", message["en"]);
        Assert.AreEqual("Fertig.", message["de"]);
    }

    private PipelineStep BuildBareStep() =>
        PipelineStep
            .Builder()
            .Id("my_step")
            .DisplayName(LocalizedText.Empty)
            .Inputs(new Dictionary<string, InputValue>())
            .OutputActions([])
            .Process(new MockPipelineProcessOptionalSingleInput())
            .Logger(loggerMock.Object)
            .Build();

    private static Dictionary<string, InputValue> SingleUploadInput() =>
        new() { ["data"] = new InputValue.StepOutputReference("upload", "OutputData") };

    private static PipelineContext ContextWith(params (string StepId, object Result)[] steps)
    {
        var stepResults = new Dictionary<string, StepResult>();
        foreach (var (stepId, result) in steps)
            stepResults[stepId] = new StepResult { Result = result };

        return new PipelineContext { Upload = Array.Empty<IPipelineFile>(), StepResults = stepResults };
    }
}
