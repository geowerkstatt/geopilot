using Geopilot.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class PipelineTest
{
    private readonly IReadOnlyList<IPipelineFile> uploadFiles = Array.Empty<IPipelineFile>();
    private Mock<ILoggerFactory> loggerFactory;
    private Mock<ILogger<Geopilot.Pipeline.Pipeline>> loggerMock;

    [TestInitialize]
    public void SetUp()
    {
        loggerFactory = new Mock<ILoggerFactory>();
        loggerMock = new Mock<ILogger<Geopilot.Pipeline.Pipeline>>();
        loggerFactory
            .Setup(lf => lf.CreateLogger(It.IsAny<string>()))
            .Returns(loggerMock.Object);
    }

    [TestMethod(DisplayName = "Pipeline State Test")]
    [DataRow(ProcessingState.Pending, new[] { StepState.Pending, StepState.Pending }, DisplayName = "all steps pending")]
    [DataRow(ProcessingState.Running, new[] { StepState.Running, StepState.Pending }, DisplayName = "steps pending and running")]
    [DataRow(ProcessingState.Running, new[] { StepState.Skipped, StepState.Running, StepState.Pending }, DisplayName = "steps pending, running and skipped")]
    [DataRow(ProcessingState.Pending, new StepState[0], DisplayName = "no steps")]
    [DataRow(ProcessingState.Failed, new[] { StepState.Success, StepState.Error, StepState.Pending }, DisplayName = "failed steps")]
    [DataRow(ProcessingState.Running, new[] { StepState.Success, StepState.Running }, DisplayName = "running steps")]
    [DataRow(ProcessingState.Running, new[] { StepState.Success, StepState.Pending }, DisplayName = "success and running steps (edge case)")]
    [DataRow(ProcessingState.Success, new[] { StepState.Success, StepState.Success }, DisplayName = "all steps success")]
    [DataRow(ProcessingState.Success, new[] { StepState.Success, StepState.Skipped, StepState.Success }, DisplayName = "all steps success or skipped")]
    public void ProcessingStateTest(ProcessingState expectedState, IEnumerable<StepState> stepStates)
    {
        var pipelineDisplayName = new Dictionary<string, string>() { { "de", "test pipeline" } };

        var steps = stepStates
            .Select(s =>
            {
                var step = new Mock<IPipelineStep>();
                step.SetupProperty(s => s.State, s);
                return step.Object;
            })
            .ToList();

        using var pipeline = Geopilot.Pipeline.Pipeline
            .Builder()
            .Id("test_pipeline")
            .DisplayName(pipelineDisplayName)
            .Steps(steps)
            .Logger(loggerMock.Object)
            .PipelineDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .JobId(Guid.NewGuid())
            .Build();

        Assert.AreEqual(expectedState, pipeline.State, "pipeline state not as expected");
    }

    [TestMethod]
    public void InteruptPipelineIfAStepFails()
    {
        var pipelineDisplayName = new Dictionary<string, string>() { { "de", "test pipeline" } };

        var firstStep = new Mock<IPipelineStep>();
        firstStep.SetupSequence(s => s.State)
            .Returns(StepState.Pending)
            .Returns(StepState.Error);

        var secondStep = new Mock<IPipelineStep>();
        secondStep.SetupProperty(s => s.State, StepState.Pending);

        var steps = new List<IPipelineStep> { firstStep.Object, secondStep.Object };

        var uploadFile = new PipelineFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var uploadFiles = new List<IPipelineFile> { uploadFile };

        using var pipeline = Geopilot.Pipeline.Pipeline
            .Builder()
            .Id("test_pipeline")
            .DisplayName(pipelineDisplayName)
            .Steps(steps)
            .Logger(loggerMock.Object)
            .PipelineDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .JobId(Guid.NewGuid())
            .Build();

        Assert.AreEqual(PipelineDelivery.Allow, pipeline.Delivery, "pipeline delivery should be allowed before running the pipeline");

        var context = pipeline.Run(uploadFiles, CancellationToken.None);

        Assert.AreEqual(PipelineDelivery.Allow, pipeline.Delivery, "pipeline delivery should be allowed after running the pipeline");

        firstStep.Verify(
            p => p.Run(It.Is<PipelineContext>(pc => pc.StepResults.Count == 0), It.IsAny<CancellationToken>()),
            Times.Once());

        secondStep.Verify(
            p => p.Run(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [TestMethod]
    public async Task PreventPipelineDeliveryIfRestrictionMatches()
    {
        var pipelineDisplayName = new Dictionary<string, string>() { { "de", "test pipeline" } };

        var step = new Mock<IPipelineStep>();

        step.SetupProperty(s => s.State, StepState.Pending);
        step.SetupGet(s => s.Id).Returns("step_id");
        StepResult stepResult = new StepResult()
        {
            Result = new DeliveryRestrictionResult { Output1 = "my_step_data" },
        };
        step.Setup(s => s.Run(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(stepResult));

        var steps = new List<IPipelineStep> { step.Object };

        var uploadFile = new PipelineFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");

        var deliveryRestrictions = new List<ConditionConfig>
        {
            new ConditionConfig
            {
                Expression = "[step_id.Output1] == 'my_step_data'",
                Message = new Dictionary<string, string>
                {
                    { "de", "Datenlieferung nicht möglich." },
                    { "en", "Delivery not possible." },
                },
            },
        };

        var uploadFiles = new List<IPipelineFile> { uploadFile };

        using var pipeline = Geopilot.Pipeline.Pipeline
            .Builder()
            .Id("test_pipeline")
            .DisplayName(pipelineDisplayName)
            .Steps(steps)
            .JobId(Guid.NewGuid())
            .DeliveryRestrictions(deliveryRestrictions)
            .Logger(loggerMock.Object)
            .PipelineDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .Build();

        Assert.AreEqual(PipelineDelivery.Allow, pipeline.Delivery, "pipeline delivery should be allowed before running the pipeline");

        var context = await pipeline.Run(uploadFiles, CancellationToken.None);

        Assert.AreEqual(PipelineDelivery.Prevent, pipeline.Delivery, "pipeline delivery should be prevented after running the pipeline");

        Assert.IsNotNull(context.DeliveryRestrictionMessage, "Context should contain a delivery restriction message.");
        Assert.AreEqual("Delivery not possible.", context.DeliveryRestrictionMessage["en"]);
        Assert.AreEqual("Datenlieferung nicht möglich.", context.DeliveryRestrictionMessage["de"]);
    }

    [TestMethod]
    public async Task PreventPipelineDeliveryWithMultipleMatchingRestrictions()
    {
        var pipelineDisplayName = new Dictionary<string, string>() { { "de", "test pipeline" } };

        var step = new Mock<IPipelineStep>();

        step.SetupProperty(s => s.State, StepState.Pending);
        step.SetupGet(s => s.Id).Returns("step_id");
        StepResult stepResult = new StepResult()
        {
            Result = new DeliveryRestrictionResult { Output1 = "my_step_data", Output2 = 42 },
        };
        step.Setup(s => s.Run(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(stepResult));

        var steps = new List<IPipelineStep> { step.Object };

        var uploadFile = new PipelineFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");

        var deliveryRestrictions = new List<ConditionConfig>
        {
            new ConditionConfig
            {
                Expression = "[step_id.Output1] == 'my_step_data'",
                Message = new Dictionary<string, string>
                {
                    { "de", "Erste Einschränkung" },
                    { "en", "First restriction" },
                },
            },
            new ConditionConfig
            {
                Expression = "[step_id.Output2] == 42",
                Message = new Dictionary<string, string>
                {
                    { "de", "Zweite Einschränkung" },
                    { "en", "Second restriction" },
                    { "fr", "Deuxième restriction" },
                },
            },
            new ConditionConfig
            {
                Expression = "[step_id.Output1] == 'no_match'",
                Message = new Dictionary<string, string>
                {
                    { "de", "Dritte Einschränkung" },
                    { "en", "Third restriction" },
                },
            },
        };

        var uploadFiles = new List<IPipelineFile> { uploadFile };

        using var pipeline = Geopilot.Pipeline.Pipeline
            .Builder()
            .Id("test_pipeline")
            .DisplayName(pipelineDisplayName)
            .Steps(steps)
            .JobId(Guid.NewGuid())
            .DeliveryRestrictions(deliveryRestrictions)
            .Logger(loggerMock.Object)
            .PipelineDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .Build();

        var context = await pipeline.Run(uploadFiles, CancellationToken.None);

        Assert.AreEqual(PipelineDelivery.Prevent, pipeline.Delivery);

        Assert.IsNotNull(context.DeliveryRestrictionMessage, "Context should contain a delivery restriction message.");
        Assert.AreEqual("First restriction, Second restriction", context.DeliveryRestrictionMessage["en"]);
        Assert.AreEqual("Erste Einschränkung, Zweite Einschränkung", context.DeliveryRestrictionMessage["de"]);
        Assert.AreEqual("Deuxième restriction", context.DeliveryRestrictionMessage["fr"]);
    }

    [TestMethod]
    public async Task RunInvokesOnStepCompletedOncePerStepInOrder()
    {
        var result1 = new StepResult();
        var result2 = new StepResult();
        var step1 = NewMockStep("step_1", result1);
        var step2 = NewMockStep("step_2", result2);

        var completed = new List<(string Id, StepResult Result)>();

        using var pipeline = BuildPipeline(step1.Object, step2.Object);
        pipeline.OnStepCompleted = (step, result, cancellationToken) =>
        {
            completed.Add((step.Id, result));
            return Task.CompletedTask;
        };

        await pipeline.Run(uploadFiles, CancellationToken.None);

        Assert.HasCount(2, completed, "OnStepCompleted should fire once per step.");
        Assert.AreEqual("step_1", completed[0].Id);
        Assert.AreSame(result1, completed[0].Result, "First callback should carry the first step's result.");
        Assert.AreEqual("step_2", completed[1].Id);
        Assert.AreSame(result2, completed[1].Result, "Second callback should carry the second step's result.");
    }

    [TestMethod]
    public async Task RunAwaitsOnStepCompletedBeforeRunningNextStep()
    {
        var events = new List<string>();
        var step1 = NewMockStep("step_1", new StepResult(), () => events.Add("run:step_1"));
        var step2 = NewMockStep("step_2", new StepResult(), () => events.Add("run:step_2"));

        using var pipeline = BuildPipeline(step1.Object, step2.Object);
        pipeline.OnStepCompleted = (step, result, cancellationToken) =>
        {
            events.Add($"hook:{step.Id}");
            return Task.CompletedTask;
        };

        await pipeline.Run(uploadFiles, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "run:step_1", "hook:step_1", "run:step_2", "hook:step_2" },
            events,
            "Each step's callback must complete before the next step runs.");
    }

    [TestMethod]
    public async Task RunInvokesOnStepCompletedOnlyForCompletedStepsWhenLaterStepThrows()
    {
        var step1 = NewMockStep("step_1", new StepResult());
        var step2 = new Mock<IPipelineStep>();
        step2.SetupGet(s => s.Id).Returns("step_2");
        step2.SetupProperty(s => s.State, StepState.Pending);
        step2.Setup(s => s.Run(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var completed = new List<string>();
        using var pipeline = BuildPipeline(step1.Object, step2.Object);
        pipeline.OnStepCompleted = (step, result, cancellationToken) =>
        {
            completed.Add(step.Id);
            return Task.CompletedTask;
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.Run(uploadFiles, CancellationToken.None));

        Assert.HasCount(1, completed);
        Assert.AreEqual("step_1", completed[0], "The throwing step must not be reported as completed.");
    }

    [TestMethod]
    public async Task RunInvokesOnStepCompletedForCompletedStepsWhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        var step1 = NewMockStep("step_1", new StepResult());
        var step2 = new Mock<IPipelineStep>();
        step2.SetupGet(s => s.Id).Returns("step_2");
        step2.SetupProperty(s => s.State, StepState.Pending);
        step2.Setup(s => s.Run(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cts.Cancel();
                return Task.FromException<StepResult>(new OperationCanceledException(cts.Token));
            });

        var completed = new List<string>();
        using var pipeline = BuildPipeline(step1.Object, step2.Object);
        pipeline.OnStepCompleted = (step, result, cancellationToken) =>
        {
            completed.Add(step.Id);
            return Task.CompletedTask;
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => pipeline.Run(uploadFiles, cts.Token));

        Assert.HasCount(1, completed);
        Assert.AreEqual("step_1", completed[0], "The cancelled step must not be reported as completed.");
    }

    [TestMethod]
    public async Task RunWithoutOnStepCompletedRunsAllSteps()
    {
        var step1 = NewMockStep("step_1", new StepResult());
        var step2 = NewMockStep("step_2", new StepResult());

        using var pipeline = BuildPipeline(step1.Object, step2.Object);

        await pipeline.Run(uploadFiles, CancellationToken.None);

        step1.Verify(s => s.Run(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()), Times.Once);
        step2.Verify(s => s.Run(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class DeliveryRestrictionResult
    {
        public string? Output1 { get; init; }

        public int Output2 { get; init; }
    }

    private static Mock<IPipelineStep> NewMockStep(string id, StepResult result, Action? onRun = null)
    {
        var step = new Mock<IPipelineStep>();
        step.SetupGet(s => s.Id).Returns(id);
        step.SetupProperty(s => s.State, StepState.Pending);
        step.Setup(s => s.Run(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                onRun?.Invoke();
                return Task.FromResult(result);
            });
        return step;
    }

    private Geopilot.Pipeline.Pipeline BuildPipeline(params IPipelineStep[] steps) =>
        Geopilot.Pipeline.Pipeline
            .Builder()
            .Id("test_pipeline")
            .DisplayName(new Dictionary<string, string> { { "de", "test pipeline" } })
            .Steps(steps.ToList())
            .Logger(loggerMock.Object)
            .PipelineDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .JobId(Guid.NewGuid())
            .Build();
}
