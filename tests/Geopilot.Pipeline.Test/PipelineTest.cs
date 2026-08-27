using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class PipelineTest
{
    private readonly IReadOnlyList<IPipelineFile> uploadFiles = Array.Empty<IPipelineFile>();
    private Mock<ILoggerFactory> loggerFactory;
    private Mock<ILogger> loggerMock;

    [TestInitialize]
    public void SetUp()
    {
        loggerFactory = new Mock<ILoggerFactory>();
        loggerMock = new Mock<ILogger>();
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
    [DataRow(ProcessingState.Warning, new[] { StepState.Success, StepState.Warning }, DisplayName = "warning with success is warning")]
    [DataRow(ProcessingState.Warning, new[] { StepState.Warning, StepState.Skipped }, DisplayName = "warning with skipped is warning")]
    [DataRow(ProcessingState.Warning, new[] { StepState.Warning, StepState.Warning }, DisplayName = "all warning is warning")]
    [DataRow(ProcessingState.Failed, new[] { StepState.Warning, StepState.Error }, DisplayName = "error wins over warning")]
    [DataRow(ProcessingState.Running, new[] { StepState.Warning, StepState.Running }, DisplayName = "warning and running is running")]
    [DataRow(ProcessingState.Running, new[] { StepState.Warning, StepState.Pending }, DisplayName = "warning mid-run stays running")]
    [DataRow(ProcessingState.DeliveryRestriction, new[] { StepState.Success, StepState.DeliveryRestriction }, DisplayName = "delivery restriction with success is delivery restriction")]
    [DataRow(ProcessingState.DeliveryRestriction, new[] { StepState.DeliveryRestriction, StepState.Skipped }, DisplayName = "delivery restriction with skipped is delivery restriction")]
    [DataRow(ProcessingState.DeliveryRestriction, new[] { StepState.DeliveryRestriction, StepState.Warning }, DisplayName = "delivery restriction wins over warning")]
    [DataRow(ProcessingState.DeliveryRestriction, new[] { StepState.DeliveryRestriction, StepState.DeliveryRestriction }, DisplayName = "all delivery restriction is delivery restriction")]
    [DataRow(ProcessingState.Failed, new[] { StepState.DeliveryRestriction, StepState.Error }, DisplayName = "error wins over delivery restriction")]
    [DataRow(ProcessingState.Running, new[] { StepState.DeliveryRestriction, StepState.Running }, DisplayName = "delivery restriction and running is running")]
    [DataRow(ProcessingState.Running, new[] { StepState.DeliveryRestriction, StepState.Pending }, DisplayName = "delivery restriction mid-run stays running")]
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

        _ = pipeline.Run(uploadFiles, CancellationToken.None);

        firstStep.Verify(
            p => p.Run(It.Is<PipelineContext>(pc => pc.StepResults.Count == 0), It.IsAny<CancellationToken>()),
            Times.Once());

        secondStep.Verify(
            p => p.Run(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()),
            Times.Never());
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

    [TestMethod]
    public async Task RunInvokesOnStepStartedBeforeEachStepInOrder()
    {
        var events = new List<string>();
        var step1 = NewMockStep("step_1", new StepResult(), () => events.Add("run:step_1"));
        var step2 = NewMockStep("step_2", new StepResult(), () => events.Add("run:step_2"));

        using var pipeline = BuildPipeline(step1.Object, step2.Object);
        pipeline.OnStepStarted = (step, cancellationToken) =>
        {
            events.Add($"started:{step.Id}");
            return Task.CompletedTask;
        };
        pipeline.OnStepCompleted = (step, result, cancellationToken) =>
        {
            events.Add($"completed:{step.Id}");
            return Task.CompletedTask;
        };

        await pipeline.Run(uploadFiles, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "started:step_1", "run:step_1", "completed:step_1", "started:step_2", "run:step_2", "completed:step_2" },
            events,
            "OnStepStarted must fire and complete before its step runs, once per step, in order.");
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

    [TestMethod]
    public void DisposeSurvivesAWorkingDirectoryThatCannotBeDeleted()
    {
        // A file where the working directory is expected makes Path.Exists report true while
        // Directory.Delete fails, on Windows as well as on Linux.
        var blockedDirectory = NewTempPath();
        File.WriteAllText(blockedDirectory, "not a directory");

        try
        {
            var step = NewDisposableStep("step_1");
            var pipeline = BuildPipelineIn(blockedDirectory, step.Object);

            pipeline.Dispose();
            pipeline.Dispose();

            step.Verify(
                s => s.Dispose(),
                Times.Once,
                "The steps are released once even when the working directory cannot be deleted.");
        }
        finally
        {
            File.Delete(blockedDirectory);
        }
    }

    [TestMethod]
    public void DisposeReleasesTheRemainingStepsWhenOneStepFails()
    {
        var directory = NewTempPath();
        Directory.CreateDirectory(directory);

        try
        {
            // A step releases the process instance a plugin provided, so its disposal runs third-party code.
            var failingStep = NewDisposableStep("failing_step");
            failingStep.Setup(s => s.Dispose()).Throws(new InvalidOperationException("process disposal failed"));
            var followingStep = NewDisposableStep("following_step");

            var pipeline = BuildPipelineIn(directory, failingStep.Object, followingStep.Object);

            pipeline.Dispose();

            followingStep.Verify(
                s => s.Dispose(),
                Times.Once,
                "A step that fails to release must not keep the following steps from being released.");
            Assert.IsFalse(Directory.Exists(directory), "The working directory is removed even when a step fails to release.");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void DisposeRunsTheCleanupOnceUnderConcurrentCalls()
    {
        var directory = NewTempPath();
        Directory.CreateDirectory(directory);

        try
        {
            var step = NewDisposableStep("step_1");
            var pipeline = BuildPipelineIn(directory, step.Object);

            // The runner's cleanup and the job retirement dispose independently, so calls can overlap.
            Parallel.For(0, 8, _ => pipeline.Dispose());

            step.Verify(s => s.Dispose(), Times.Once, "Concurrent disposals must run the cleanup exactly once.");
            Assert.IsFalse(Directory.Exists(directory), "The working directory is removed by the one disposal that runs.");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static string NewTempPath() => Path.Combine(Path.GetTempPath(), $"geopilot-test-{Guid.NewGuid()}");

    private static Mock<IPipelineStep> NewDisposableStep(string id)
    {
        var step = new Mock<IPipelineStep>();
        step.SetupGet(s => s.Id).Returns(id);
        return step;
    }

    private Geopilot.Pipeline.Pipeline BuildPipeline(params IPipelineStep[] steps) =>
        BuildPipelineIn(NewTempPath(), steps);

    private Geopilot.Pipeline.Pipeline BuildPipelineIn(string pipelineDirectory, params IPipelineStep[] steps) =>
        Geopilot.Pipeline.Pipeline
            .Builder()
            .Id("test_pipeline")
            .DisplayName(new Dictionary<string, string> { { "de", "test pipeline" } })
            .Steps(steps.ToList())
            .Logger(loggerMock.Object)
            .PipelineDirectory(pipelineDirectory)
            .JobId(Guid.NewGuid())
            .Build();
}
