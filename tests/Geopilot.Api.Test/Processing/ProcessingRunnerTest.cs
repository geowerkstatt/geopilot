using Geopilot.Api.FileAccess;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Threading.Channels;

namespace Geopilot.Api.Test.Processing;

[TestClass]
public class ProcessingRunnerTest
{
    private readonly ILogger pipelineLogger = Mock.Of<ILogger>();
    private readonly List<Guid> createdJobIds = new();
    private readonly List<string> tempFiles = new();

    private PhysicalDownloadFileStore downloadStore;
    private PhysicalAssetFileStore assetStore;
    private PhysicalVisualizationFileStore visualizationStore;
    private IServiceScopeFactory scopeFactory;

    [TestInitialize]
    public void Initialize()
    {
        downloadStore = new PhysicalDownloadFileStore(AssemblyInitialize.TestDirectoryProvider);
        assetStore = new PhysicalAssetFileStore(AssemblyInitialize.TestDirectoryProvider);
        visualizationStore = new PhysicalVisualizationFileStore(AssemblyInitialize.TestDirectoryProvider);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(p => p.GetService(typeof(IDownloadFileStore))).Returns(downloadStore);
        serviceProvider.Setup(p => p.GetService(typeof(IAssetFileStore))).Returns(assetStore);
        serviceProvider.Setup(p => p.GetService(typeof(IVisualizationFileStore))).Returns(visualizationStore);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scope.Object);
        scopeFactory = scopeFactoryMock.Object;
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var jobId in createdJobIds)
        {
            downloadStore.DeleteJob(jobId);
            assetStore.DeleteJob(jobId);
            visualizationStore.DeleteJob(jobId);
        }

        foreach (var path in tempFiles.Where(File.Exists))
            File.Delete(path);
    }

    private sealed class FileEmittingProcess
    {
        private readonly string filePath;
        private readonly string originalFileName;

        public FileEmittingProcess(string filePath, string originalFileName)
        {
            this.filePath = filePath;
            this.originalFileName = originalFileName;
        }

        [PipelineProcessRun]
        public Task<FileEmittingProcessResult> RunAsync(CancellationToken cancellationToken)
            => Task.FromResult(new FileEmittingProcessResult { Result = new PipelineFile(filePath, originalFileName) });
    }

    private sealed class FileEmittingProcessResult
    {
        public required IPipelineFile Result { get; init; }
    }

    private sealed class BlockingProcess
    {
        private readonly Task gate;

        public BlockingProcess(Task gate) => this.gate = gate;

        [PipelineProcessRun]
        public async Task<EmptyResult> RunAsync(CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);
            return new EmptyResult();
        }
    }

    private sealed class ThrowingProcess
    {
        [PipelineProcessRun]
        public Task<EmptyResult> RunAsync(CancellationToken cancellationToken)
            => Task.FromException<EmptyResult>(new InvalidOperationException("step failed"));
    }

    private sealed class EmptyResult
    {
    }

    // Backs the FileStepResult / ObjectStepResult helpers: a result type exposing a single output under
    // the property name "Output". The paired step tags that same property via BuildBareStep.
    private sealed class SingleOutputResult
    {
        public object? Output { get; init; }
    }

    private sealed class TestVisualizationConfig
    {
        public string Data { get; init; }
    }

    [TestMethod]
    public void ExtractStepDownloadsWritesDownloadFileToDownloadStoreOnly()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Download);
        var stepResult = FileStepResult("result.log", "log-content");

        runner.ExtractStepDownloads(jobId, step, stepResult);

        Assert.HasCount(1, step.Downloads);
        var persisted = step.Downloads[0];
        Assert.AreEqual("result.log", persisted.OriginalFileName);
        Assert.AreEqual("step_1_result.log", persisted.PersistedFileName);
        Assert.IsTrue(downloadStore.Exists(jobId, persisted.PersistedFileName));
        Assert.IsFalse(assetStore.Exists(jobId, persisted.PersistedFileName), "Download files must not be written to the asset store.");
        Assert.AreEqual("log-content", File.ReadAllText(downloadStore.GetPath(jobId, persisted.PersistedFileName)));
    }

    [TestMethod]
    public void ExtractStepDownloadsWritesEachFileWhenDownloadOutputIsAFileCollection()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Download);
        var files = new IPipelineFile[]
        {
            new PipelineFile(WriteTempFile("first_content"), "first.log"),
            new PipelineFile(WriteTempFile("second_content"), "second.log"),
        };

        var stepResult = ObjectStepResult(files);

        runner.ExtractStepDownloads(jobId, step, stepResult);

        Assert.HasCount(2, step.Downloads);
        var persistedFirst = step.Downloads[0];
        var persistedSecond = step.Downloads[1];

        Assert.AreEqual("first.log", persistedFirst.OriginalFileName);
        Assert.AreEqual("step_1_first.log", persistedFirst.PersistedFileName);
        Assert.AreEqual("second.log", persistedSecond.OriginalFileName);
        Assert.AreEqual("step_1_second.log", persistedSecond.PersistedFileName);

        Assert.IsTrue(downloadStore.Exists(jobId, persistedFirst.PersistedFileName));
        Assert.IsTrue(downloadStore.Exists(jobId, persistedSecond.PersistedFileName));
        Assert.IsFalse(assetStore.Exists(jobId, persistedFirst.PersistedFileName), "Download files must not be written to the asset store.");
        Assert.IsFalse(assetStore.Exists(jobId, persistedSecond.PersistedFileName), "Download files must not be written to the asset store.");
        Assert.AreEqual("first_content", File.ReadAllText(downloadStore.GetPath(jobId, persistedFirst.PersistedFileName)));
        Assert.AreEqual("second_content", File.ReadAllText(downloadStore.GetPath(jobId, persistedSecond.PersistedFileName)));
    }

    [TestMethod]
    public void ExtractStepDownloadsWritesVisualizationToVisualizationStoreOnly()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Visualization);
        Visualization<TestVisualizationConfig> visualization = new("testViz", new TestVisualizationConfig { Data = "Hello World." });
        var stepResult = ObjectStepResult(visualization);

        runner.ExtractStepDownloads(jobId, step, stepResult);

        Assert.HasCount(1, step.Visualizations);
        var persisted = step.Visualizations[0];
        Assert.AreEqual("Output.json", persisted.OriginalFileName);
        Assert.AreEqual("step_1_Output.json", persisted.PersistedFileName);
        Assert.IsTrue(visualizationStore.Exists(jobId, persisted.PersistedFileName));
        Assert.IsFalse(downloadStore.Exists(jobId, persisted.PersistedFileName), "Visualizations must not be written to the download store.");
        Assert.IsEmpty(step.Downloads);

        var json = File.ReadAllText(visualizationStore.GetPath(jobId, persisted.PersistedFileName));
        StringAssert.Contains(json, "{\"data\":\"Hello World.\"}");
    }

    [TestMethod]
    public void ExtractStepDownloadsIgnoresStepThatDidNotRun()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Download, OutputAction.Visualization);
        var stepResult = new StepResult();

        runner.ExtractStepDownloads(jobId, step, stepResult);

        Assert.IsEmpty(step.Downloads);
        Assert.IsEmpty(step.Visualizations);
    }

    [TestMethod]
    public void ExtractStepDownloadsFailsWhenVisualizationOutputIsNotAnEnvelope()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Visualization);
        var stepResult = ObjectStepResult("not a visualization");

        var exception = Assert.ThrowsExactly<PipelineRunException>(() => runner.ExtractStepDownloads(jobId, step, stepResult));

        Assert.Contains("Visualization", exception.Message);
        Assert.IsEmpty(step.Visualizations);
    }

    [TestMethod]
    public void ExtractStepDownloadsFailsWhenDownloadOutputIsNotAFile()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Download);
        var stepResult = ObjectStepResult("not a file");

        var exception = Assert.ThrowsExactly<PipelineRunException>(() => runner.ExtractStepDownloads(jobId, step, stepResult));

        Assert.Contains("Download", exception.Message);
        Assert.IsEmpty(step.Downloads);
    }

    [TestMethod]
    public void ExtractStepDownloadsFailsWhenPropertyDoesNotExistOnResult()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());

        var step = PipelineStep
            .Builder()
            .Id("step_1")
            .DisplayName(LocalizedText.Empty)
            .Inputs(new Dictionary<string, InputValue>())
            .OutputActions([new OutputActionConfig { Property = "DoesNotExist", Actions = new HashSet<OutputAction> { OutputAction.Download } }])
            .Process(new object())
            .Logger(pipelineLogger)
            .Build();
        var stepResult = ObjectStepResult("some_data");

        Assert.ThrowsExactly<ArgumentException>(() => runner.ExtractStepDownloads(jobId, step, stepResult));
    }

    [TestMethod]
    public void ExtractDeliveryFilesWritesDeliveryFileToAssetStoreOnly()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Delivery);
        var stepResult = FileStepResult("data.xtf", "delivery-content");
        using var pipeline = BuildPipeline(jobId, step);
        var context = ContextWith(step, stepResult);

        runner.ExtractDeliveryFiles(pipeline, context);

        Assert.HasCount(1, step.DeliveryFiles);
        var persisted = step.DeliveryFiles[0];
        Assert.AreEqual("data.xtf", persisted.OriginalFileName);
        Assert.AreEqual("step_1_data.xtf", persisted.PersistedFileName);
        Assert.IsTrue(assetStore.Exists(jobId, persisted.PersistedFileName));
        Assert.IsFalse(downloadStore.Exists(jobId, persisted.PersistedFileName), "Delivery files must not be written to the download store.");
        Assert.IsEmpty(step.Downloads);
    }

    [TestMethod]
    public void FileTaggedDownloadAndDeliveryIsWrittenToBothStoresUnderTheSameName()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Download, OutputAction.Delivery);
        var stepResult = FileStepResult("report.pdf", "report-content");
        using var pipeline = BuildPipeline(jobId, step);
        var context = ContextWith(step, stepResult);

        runner.ExtractStepDownloads(jobId, step, stepResult);
        runner.ExtractDeliveryFiles(pipeline, context);

        Assert.HasCount(1, step.Downloads);
        Assert.HasCount(1, step.DeliveryFiles);
        Assert.AreEqual(
            step.Downloads[0].PersistedFileName,
            step.DeliveryFiles[0].PersistedFileName,
            "A file tagged for both actions should be persisted under the same name in both stores.");
        Assert.IsTrue(downloadStore.Exists(jobId, step.Downloads[0].PersistedFileName));
        Assert.IsTrue(assetStore.Exists(jobId, step.DeliveryFiles[0].PersistedFileName));
    }

    [TestMethod]
    public async Task DownloadFromCompletedStepIsAvailableWhileLaterStepStillRuns()
    {
        var jobId = NewJob();
        var gate = new TaskCompletionSource();

        var step1 = BuildEmittingStep("step_1", "log", "first.log", "first-content", OutputAction.Download);
        var step2 = BuildBlockingStep("step_2", gate.Task);
        using var pipeline = BuildPipeline(jobId, step1, step2);
        var job = new ProcessingJob(jobId, new List<ProcessingJobFile>(), 1, DateTime.UtcNow) { Pipeline = pipeline };

        var (runner, store) = CreateRunnerWithStore(pipeline);

        await runner.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => step1.Downloads.Count > 0, TimeSpan.FromSeconds(10));

            // Mid-run: step 1's download exists on disk and is exposed via the API, while step 2 has not finished.
            Assert.AreEqual(StepState.Success, step1.State);
            Assert.AreNotEqual(StepState.Success, step2.State, "Step 2 must still be running while step 1's download is offered.");
            Assert.AreEqual(ProcessingState.Running, pipeline.State);
            store.Verify(s => s.PipelineFinished(It.IsAny<Guid>(), It.IsAny<ProcessingState>()), Times.Never);

            var persistedName = step1.Downloads[0].PersistedFileName;
            Assert.IsTrue(downloadStore.Exists(jobId, persistedName), "Step 1's download must be on disk before the pipeline finishes.");

            var response = job.ToResponse(
                (id, file) => new Uri($"https://localhost/api/v2/processing/{id}/files/{file}"),
                (id, file) => new Uri($"https://localhost/api/v2/processing/{id}/visualizations/{file}"));
            var step1Response = response.Steps.Single(s => s.Id == "step_1");
            Assert.HasCount(1, step1Response.Downloads);
            Assert.AreEqual("first.log", step1Response.Downloads[0].OriginalFileName);

            gate.SetResult();
            await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.AreEqual(ProcessingState.Success, pipeline.State);
            Assert.IsTrue(downloadStore.Exists(jobId, persistedName));
            store.Verify(s => s.PipelineFinished(jobId, ProcessingState.Success), Times.Once);
        }
        finally
        {
            gate.TrySetResult();
            await runner.StopAsync(CancellationToken.None);
        }
    }

    [TestMethod]
    public async Task DownloadFromCompletedStepSurvivesLaterStepFailure()
    {
        var jobId = NewJob();
        var step1 = BuildEmittingStep("step_1", "log", "first.log", "first-content", OutputAction.Download);
        var step2 = BuildThrowingStep("step_2");
        using var pipeline = BuildPipeline(jobId, step1, step2);

        var (runner, store) = CreateRunnerWithStore(pipeline);

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        Assert.HasCount(1, step1.Downloads);
        Assert.IsTrue(downloadStore.Exists(jobId, step1.Downloads[0].PersistedFileName), "Earlier step's download must survive a later step failure.");
        store.Verify(s => s.MarkAsFailed(jobId), Times.Once);
    }

    [TestMethod]
    public async Task DownloadFromCompletedStepSurvivesJobTimeout()
    {
        var jobId = NewJob();
        var gate = new TaskCompletionSource();

        var step1 = BuildEmittingStep("step_1", "log", "first.log", "first-content", OutputAction.Download);
        var step2 = BuildBlockingStep("step_2", gate.Task);
        using var pipeline = BuildPipeline(jobId, step1, step2);

        var (runner, store) = CreateRunnerWithStore(pipeline, TimeSpan.FromSeconds(2));

        await runner.StartAsync(CancellationToken.None);
        try
        {
            await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally
        {
            gate.TrySetResult();
            await runner.StopAsync(CancellationToken.None);
        }

        Assert.HasCount(1, step1.Downloads);
        Assert.IsTrue(downloadStore.Exists(jobId, step1.Downloads[0].PersistedFileName), "Pre-timeout step's download must survive the job timeout.");
        store.Verify(s => s.PipelineFinished(jobId, ProcessingState.Cancelled), Times.Once);
    }

    [TestMethod]
    public async Task DeliveryFilesAreStagedWhenPipelineSucceedsAndDeliveryAllowed()
    {
        var jobId = NewJob();
        var step = BuildEmittingStep("step_1", "payload", "data.xtf", "delivery-content", OutputAction.Delivery);
        using var pipeline = BuildPipeline(jobId, step);

        var (runner, store) = CreateRunnerWithStore(pipeline);

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        Assert.AreEqual(ProcessingState.Success, pipeline.State);
        Assert.HasCount(1, step.DeliveryFiles);
        Assert.IsTrue(assetStore.Exists(jobId, "step_1_data.xtf"));
        store.Verify(s => s.PipelineFinished(jobId, ProcessingState.Success), Times.Once);
    }

    [TestMethod]
    public async Task DeliveryFilesAreNotStagedWhenAFailConditionAbortsThePipeline()
    {
        var jobId = NewJob();
        var step1 = BuildEmittingStep("step_1", "payload", "data.xtf", "delivery-content", OutputAction.Delivery);
        var step2 = BuildFailConditionStep("step_2");
        using var pipeline = BuildPipeline(jobId, step1, step2);

        var (runner, store) = CreateRunnerWithStore(pipeline);

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        Assert.AreEqual(ProcessingState.Failed, pipeline.State);
        Assert.IsEmpty(step1.DeliveryFiles, "No delivery files may be staged when the pipeline does not complete successfully.");
        Assert.IsFalse(assetStore.Exists(jobId, "step_1_data.xtf"), "No partial delivery may be written to the asset store on failure.");
        store.Verify(s => s.PipelineFinished(jobId, ProcessingState.Failed), Times.Once);
    }

    [TestMethod]
    public async Task DeliveryFilesAreNotStagedWhenDeliveryIsPrevented()
    {
        var jobId = NewJob();
        var step = BuildEmittingStep("step_1", "payload", "data.xtf", "delivery-content", OutputAction.Delivery);
        using var pipeline = BuildRestrictedPipeline(jobId, "1 == 1", step);

        var (runner, store) = CreateRunnerWithStore(pipeline);

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        Assert.AreEqual(ProcessingState.Success, pipeline.State);
        Assert.AreEqual(PipelineDelivery.Prevent, pipeline.Delivery);
        Assert.IsEmpty(step.DeliveryFiles, "No delivery files may be staged when delivery is prevented by a restriction.");
        Assert.IsFalse(assetStore.Exists(jobId, "step_1_data.xtf"), "No delivery may be written to the asset store when delivery is prevented.");
        store.Verify(s => s.PipelineFinished(jobId, ProcessingState.Success), Times.Once);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail("Condition was not met within the timeout.");
            await Task.Delay(20);
        }
    }

    private static PipelineContext ContextWith(PipelineStep step, StepResult stepResult) =>
        new PipelineContext
        {
            Upload = Array.Empty<IPipelineFile>(),
            StepResults = new Dictionary<string, StepResult> { { step.Id, stepResult } },
        };

    private Guid NewJob()
    {
        var jobId = Guid.NewGuid();
        createdJobIds.Add(jobId);
        return jobId;
    }

    private ProcessingRunner CreateRunner(IProcessingJobStore jobStore, TimeSpan? jobTimeout = null) =>
        new ProcessingRunner(
            Mock.Of<ILogger<ProcessingRunner>>(),
            jobStore,
            scopeFactory,
            Options.Create(new ProcessingOptions { JobTimeout = jobTimeout ?? TimeSpan.FromMinutes(5) }));

    private (ProcessingRunner Runner, Mock<IProcessingJobStore> Store) CreateRunnerWithStore(IPipeline pipeline, TimeSpan? jobTimeout = null)
    {
        var channel = Channel.CreateUnbounded<ProcessingWorkItem>();
        channel.Writer.TryWrite(new ProcessingWorkItem(pipeline, Array.Empty<IPipelineFile>()));
        channel.Writer.Complete();

        var store = new Mock<IProcessingJobStore>();
        store.SetupGet(s => s.ProcessingQueue).Returns(channel.Reader);

        return (CreateRunner(store.Object, jobTimeout), store);
    }

    private string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"geopilot-test-{Guid.NewGuid()}.tmp");
        File.WriteAllText(path, content);
        tempFiles.Add(path);
        return path;
    }

    // The tagging (which action applies) now lives on the step's OutputActions; the step result carries
    // only the raw process result. Both agree on the property name "Output": the step tags it, the result
    // exposes it under that name, and the runner resolves the data via StepResult.ExtractProperty.
    private StepResult FileStepResult(string originalFileName, string content) =>
        new StepResult { Result = new SingleOutputResult { Output = new PipelineFile(WriteTempFile(content), originalFileName) } };

    private static StepResult ObjectStepResult(object data) =>
        new StepResult { Result = new SingleOutputResult { Output = data } };

    private PipelineStep BuildBareStep(string id, params OutputAction[] actions) =>
        PipelineStep
            .Builder()
            .Id(id)
            .DisplayName(LocalizedText.Empty)
            .Inputs(new Dictionary<string, InputValue>())
            .OutputActions(actions.Length == 0
                ? []
                : [new OutputActionConfig { Property = "Output", Actions = new HashSet<OutputAction>(actions) }])
            .Process(new object())
            .Logger(pipelineLogger)
            .Build();

    private PipelineStep BuildEmittingStep(string id, string outputKey, string originalFileName, string content, params OutputAction[] actions) =>
        PipelineStep
            .Builder()
            .Id(id)
            .DisplayName(LocalizedText.Empty)
            .Inputs(new Dictionary<string, InputValue>())
            .OutputActions([new OutputActionConfig { Property = "Result", Actions = new HashSet<OutputAction>(actions) }])
            .Process(new FileEmittingProcess(WriteTempFile(content), originalFileName))
            .Logger(pipelineLogger)
            .Build();

    private PipelineStep BuildBlockingStep(string id, Task gate) =>
        PipelineStep
            .Builder()
            .Id(id)
            .DisplayName(LocalizedText.Empty)
            .Inputs(new Dictionary<string, InputValue>())
            .OutputActions([])
            .Process(new BlockingProcess(gate))
            .Logger(pipelineLogger)
            .Build();

    private PipelineStep BuildThrowingStep(string id) =>
        PipelineStep
            .Builder()
            .Id(id)
            .DisplayName(LocalizedText.Empty)
            .Inputs(new Dictionary<string, InputValue>())
            .OutputActions([])
            .Process(new ThrowingProcess())
            .Logger(pipelineLogger)
            .Build();

    private PipelineStep BuildFailConditionStep(string id) =>
        PipelineStep
            .Builder()
            .Id(id)
            .DisplayName(LocalizedText.Empty)
            .Inputs(new Dictionary<string, InputValue>())
            .OutputActions([])
            .StepConditions(new PipelineStepConditionsConfig
            {
                Pre = new PipelineStepPreConditionConfig
                {
                    FailConditions = new List<ConditionConfig> { new ConditionConfig { Expression = "1 == 1" } },
                },
            })
            .Process(new object())
            .Logger(pipelineLogger)
            .Build();

    private Geopilot.Pipeline.Pipeline BuildRestrictedPipeline(Guid jobId, string restrictionExpression, params PipelineStep[] steps) =>
        Geopilot.Pipeline.Pipeline
            .Builder()
            .Id("test_pipeline")
            .DisplayName(LocalizedText.Empty)
            .Steps(steps.Cast<IPipelineStep>().ToList())
            .DeliveryRestrictions(new List<ConditionConfig> { new ConditionConfig { Expression = restrictionExpression } })
            .Logger(pipelineLogger)
            .PipelineDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .JobId(jobId)
            .Build();

    private Geopilot.Pipeline.Pipeline BuildPipeline(Guid jobId, params PipelineStep[] steps) =>
        Geopilot.Pipeline.Pipeline
            .Builder()
            .Id("test_pipeline")
            .DisplayName(LocalizedText.Empty)
            .Steps(steps.Cast<IPipelineStep>().ToList())
            .Logger(pipelineLogger)
            .PipelineDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .JobId(jobId)
            .Build();
}
