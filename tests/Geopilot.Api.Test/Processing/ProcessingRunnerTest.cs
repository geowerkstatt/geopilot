using Geopilot.Api.FileAccess;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
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
    private Mock<IUploadOrchestrationService> orchestrationServiceMock;
    private IServiceScopeFactory scopeFactory;

    [TestInitialize]
    public void Initialize()
    {
        downloadStore = new PhysicalDownloadFileStore(AssemblyInitialize.TestDirectoryProvider);
        assetStore = new PhysicalAssetFileStore(AssemblyInitialize.TestDirectoryProvider);
        visualizationStore = new PhysicalVisualizationFileStore(AssemblyInitialize.TestDirectoryProvider);
        orchestrationServiceMock = new Mock<IUploadOrchestrationService>();
        orchestrationServiceMock.Setup(c => c.ReleaseUploadAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(p => p.GetService(typeof(IDownloadFileStore))).Returns(downloadStore);
        serviceProvider.Setup(p => p.GetService(typeof(IAssetFileStore))).Returns(assetStore);
        serviceProvider.Setup(p => p.GetService(typeof(IVisualizationFileStore))).Returns(visualizationStore);
        serviceProvider.Setup(p => p.GetService(typeof(IUploadOrchestrationService))).Returns(orchestrationServiceMock.Object);

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

    private sealed class PassthroughProcess
    {
        [PipelineProcessRun]
        public Task<PassthroughProcessResult> RunAsync(IPipelineFile[] files)
            => Task.FromResult(new PassthroughProcessResult { Result = files });
    }

    private sealed class PassthroughProcessResult
    {
        public required IPipelineFile[] Result { get; init; }
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

    /// <summary>
    /// Stands in for a file that still has to be fetched from remote storage and never becomes ready.
    /// It only completes when the token it was handed is cancelled, so a caller that drops the token
    /// hangs instead of failing.
    /// </summary>
    private sealed class NeverReadyFile : IPipelineFile
    {
        public string OriginalFileName => "never-ready.log";

        public string OriginalFileNameWithoutExtension => "never-ready";

        public string FileExtension => "log";

        public string OriginalRelativePath => string.Empty;

        public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }

        public FileStream OpenWriteFileStream() => throw new NotSupportedException();

        public Task<string> GetLocalPathAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    [TestMethod]
    public async Task ExtractStepDownloadsWritesDownloadFileToDownloadStoreOnly()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Download);
        var stepResult = FileStepResult("result.log", "log-content");

        await runner.ExtractStepDownloadsAsync(jobId, step, stepResult);

        Assert.HasCount(1, step.Downloads);
        var persisted = step.Downloads[0];
        Assert.AreEqual("result.log", persisted.OriginalFileName);
        Assert.AreEqual("step_1_result.log", persisted.PersistedFileName);
        Assert.IsTrue(downloadStore.Exists(jobId, persisted.PersistedFileName));
        Assert.IsFalse(assetStore.Exists(jobId, persisted.PersistedFileName), "Download files must not be written to the asset store.");
        Assert.AreEqual("log-content", File.ReadAllText(downloadStore.GetPath(jobId, persisted.PersistedFileName)));
    }

    [TestMethod]
    public async Task ExtractStepDownloadsWritesEachFileWhenDownloadOutputIsAFileCollection()
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

        await runner.ExtractStepDownloadsAsync(jobId, step, stepResult);

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
    public async Task ExtractStepDownloadsWritesVisualizationToVisualizationStoreOnly()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Visualization);
        Visualization<TestVisualizationConfig> visualization = new("testViz", new TestVisualizationConfig { Data = "Hello World." });
        var stepResult = ObjectStepResult(visualization);

        await runner.ExtractStepDownloadsAsync(jobId, step, stepResult);

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
    public async Task ExtractStepDownloadsIgnoresStepThatDidNotRun()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Download, OutputAction.Visualization);
        var stepResult = new StepResult();

        await runner.ExtractStepDownloadsAsync(jobId, step, stepResult);

        Assert.IsEmpty(step.Downloads);
        Assert.IsEmpty(step.Visualizations);
    }

    [TestMethod]
    public async Task ExtractStepDownloadsFailsWhenVisualizationOutputIsNotAnEnvelope()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Visualization);
        var stepResult = ObjectStepResult("not a visualization");

        var exception = await Assert.ThrowsExactlyAsync<PipelineRunException>(() => runner.ExtractStepDownloadsAsync(jobId, step, stepResult));

        Assert.Contains("Visualization", exception.Message);
        Assert.IsEmpty(step.Visualizations);
    }

    [TestMethod]
    public async Task ExtractStepDownloadsFailsWhenDownloadOutputIsNotAFile()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Download);
        var stepResult = ObjectStepResult("not a file");

        var exception = await Assert.ThrowsExactlyAsync<PipelineRunException>(() => runner.ExtractStepDownloadsAsync(jobId, step, stepResult));

        Assert.Contains("Download", exception.Message);
        Assert.IsEmpty(step.Downloads);
    }

    [TestMethod]
    public async Task ExtractStepDownloadsFailsWhenPropertyDoesNotExistOnResult()
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

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => runner.ExtractStepDownloadsAsync(jobId, step, stepResult));
    }

    [TestMethod]
    public async Task ExtractDeliveryFilesWritesDeliveryFileToAssetStoreOnly()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Delivery);
        var stepResult = FileStepResult("data.xtf", "delivery-content");
        using var pipeline = BuildPipeline(jobId, step);
        var context = ContextWith(step, stepResult);

        await runner.ExtractDeliveryFilesAsync(pipeline, context);

        Assert.HasCount(1, step.DeliveryFiles);
        var persisted = step.DeliveryFiles[0];
        Assert.AreEqual("data.xtf", persisted.OriginalFileName);
        Assert.AreEqual("step_1_data.xtf", persisted.PersistedFileName);
        Assert.IsTrue(assetStore.Exists(jobId, persisted.PersistedFileName));
        Assert.IsFalse(downloadStore.Exists(jobId, persisted.PersistedFileName), "Delivery files must not be written to the download store.");
        Assert.IsEmpty(step.Downloads);
    }

    [TestMethod]
    public async Task DeliveryFilesSetFromUploadPerFile()
    {
        var jobId = NewJob();
        var upload = new PipelineFile(WriteTempFile("upload-content"), "uploaded.xtf");

        var passthrough = BuildUploadPassthroughStep("passthrough");
        var producer = BuildEmittingStep("producer", "payload", "report.log", "report-content", OutputAction.Delivery);
        using var pipeline = BuildPipeline(jobId, passthrough, producer);

        var (runner, store) = CreateRunnerWithStore(pipeline, uploads: [upload]);

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        Assert.AreEqual(ProcessingState.Success, pipeline.State);
        Assert.IsTrue(
            passthrough.DeliveryFiles.Single().FromUpload,
            "A delivered upload forwarded by a step must be marked as coming from the upload.");
        Assert.IsFalse(
            producer.DeliveryFiles.Single().FromUpload,
            "A delivered file produced by a step must not be marked as coming from the upload.");
    }

    [TestMethod]
    public async Task FileTaggedDownloadAndDeliveryIsWrittenToBothStoresUnderTheSameName()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Download, OutputAction.Delivery);
        var stepResult = FileStepResult("report.pdf", "report-content");
        using var pipeline = BuildPipeline(jobId, step);
        var context = ContextWith(step, stepResult);

        await runner.ExtractStepDownloadsAsync(jobId, step, stepResult);
        await runner.ExtractDeliveryFilesAsync(pipeline, context);

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
        var job = new ProcessingJob(jobId, Guid.NewGuid(), 1, DateTime.UtcNow) { Pipeline = pipeline };

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
            Assert.HasCount(0, step1Response.Deliveries);

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
    public async Task StepResponseContainsDelivery()
    {
        var jobId = NewJob();
        var step1 = BuildEmittingStep("step_1", "log", "first.log", "first-content", OutputAction.Delivery);
        using var pipeline = BuildPipeline(jobId, step1);
        var job = new ProcessingJob(jobId, Guid.NewGuid(), 1, DateTime.UtcNow) { Pipeline = pipeline };

        var (runner, store) = CreateRunnerWithStore(pipeline);

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        var response = job.ToResponse(
            (id, file) => new Uri($"https://localhost/api/v2/processing/{id}/files/{file}"),
            (id, file) => new Uri($"https://localhost/api/v2/processing/{id}/visualizations/{file}"));

        Assert.HasCount(2, response.Steps);
        Assert.AreEqual("preflight", response.Steps[0].Id);
        var step1Response = response.Steps[1];
        Assert.AreEqual("step_1", step1Response.Id);
        Assert.HasCount(0, step1Response.Downloads);
        Assert.HasCount(1, step1Response.Deliveries);
        Assert.AreEqual("first.log", step1Response.Deliveries[0]);
        store.Verify(s => s.PipelineFinished(jobId, ProcessingState.Success), Times.Once);
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
        store.Verify(s => s.TryMarkAsFailed(jobId), Times.Once);
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
        store.Verify(s => s.TryPipelineFinished(jobId, ProcessingState.Cancelled), Times.Once);
    }

    [TestMethod]
    public async Task ExtractStepDownloadsPassesTheJobsTokenToTheFetch()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Download);
        var stepResult = ObjectStepResult(new NeverReadyFile());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // A matcher hands the uploaded file on unchanged, so a matcher output tagged for download can
        // trigger the first fetch right here. Without the token that fetch waits forever, and because
        // Pipeline.Run awaits this callback the job would outlive its timeout and the host shutdown.
        await AssertObservesCancellationAsync(runner.ExtractStepDownloadsAsync(jobId, step, stepResult, cts.Token));
    }

    [TestMethod]
    public async Task ExtractDeliveryFilesPassesTheJobsTokenToTheFetch()
    {
        var jobId = NewJob();
        using var runner = CreateRunner(Mock.Of<IProcessingJobStore>());
        var step = BuildBareStep("step_1", OutputAction.Delivery);
        var stepResult = ObjectStepResult(new NeverReadyFile());
        using var pipeline = BuildPipeline(jobId, step);
        var context = ContextWith(step, stepResult);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await AssertObservesCancellationAsync(runner.ExtractDeliveryFilesAsync(pipeline, context, cts.Token));
    }

    /// <summary>
    /// Asserts that an extraction fed an already-cancelled token gives up instead of waiting on a fetch
    /// that never completes. Races the call against a deadline rather than relying on a test timeout, so
    /// a regression fails with this message instead of wedging the whole test run.
    /// </summary>
    private static async Task AssertObservesCancellationAsync(Task extraction)
    {
        var finished = await Task.WhenAny(extraction, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.AreSame(extraction, finished, "the fetch did not observe the job's cancellation token and kept waiting");
        await Assert.ThrowsAsync<OperationCanceledException>(() => extraction);
    }

    [TestMethod]
    public async Task UploadIsReleasedWhenTheRunCannotBeDelivered()
    {
        var jobId = NewJob();
        var uploadId = Guid.NewGuid();
        var step = BuildThrowingStep("step_1");
        using var pipeline = BuildPipeline(jobId, step);

        var (runner, store) = CreateRunnerWithStore(pipeline);
        store.Setup(s => s.GetJob(jobId)).Returns(FinishedJob(jobId, uploadId, ProcessingState.Failed));

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        // The originals will never be archived, so they can go right away.
        orchestrationServiceMock.Verify(c => c.ReleaseUploadAsync(uploadId), Times.Once);
    }

    [TestMethod]
    public async Task UploadIsKeptWhenTheRunWasInterruptedByAHostShutdown()
    {
        var jobId = NewJob();
        var uploadId = Guid.NewGuid();
        var step = BuildThrowingStep("step_1");
        using var pipeline = BuildPipeline(jobId, step);

        var (runner, store) = CreateRunnerWithStore(pipeline);

        // A host shutdown leaves the job in Running: nobody has decided yet whether it mattered, so the
        // originals have to survive. UploadCleanupService's age-based sweep is what collects them.
        store.Setup(s => s.GetJob(jobId)).Returns(FinishedJob(jobId, uploadId, ProcessingState.Running));

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        orchestrationServiceMock.Verify(c => c.ReleaseUploadAsync(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task UploadIsKeptWhenTheRunCanStillBeDelivered()
    {
        var jobId = NewJob();
        var uploadId = Guid.NewGuid();
        var step = BuildEmittingStep("step_1", "payload", "data.xtf", "delivery-content", OutputAction.Delivery);
        using var pipeline = BuildPipeline(jobId, step);

        var (runner, store) = CreateRunnerWithStore(pipeline);
        store.Setup(s => s.GetJob(jobId)).Returns(FinishedJob(jobId, uploadId, ProcessingState.Success));

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        // Declaring the delivery archives every original as primary data, so they must stay reachable.
        orchestrationServiceMock.Verify(c => c.ReleaseUploadAsync(It.IsAny<Guid>()), Times.Never);
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
    public async Task DeliveryFilesAreStagedWhenPipelineWarnsAndDeliveryAllowed()
    {
        var jobId = NewJob();
        var step = BuildWarningDeliveryStep("step_1", "payload", "data.xtf", "delivery-content");
        using var pipeline = BuildPipeline(jobId, step);

        var (runner, store) = CreateRunnerWithStore(pipeline);

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        Assert.AreEqual(ProcessingState.Warning, pipeline.State);
        Assert.HasCount(1, step.DeliveryFiles);
        Assert.IsTrue(assetStore.Exists(jobId, "step_1_data.xtf"));
        store.Verify(s => s.PipelineFinished(jobId, ProcessingState.Warning), Times.Once);
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
    public async Task DeliveryFilesAreNotStagedWhenDeliveryIsRestricted()
    {
        var jobId = NewJob();
        var step = BuildRestrictDeliveryStep("step_1", "payload", "data.xtf", "delivery-content");
        using var pipeline = BuildPipeline(jobId, step);

        var (runner, store) = CreateRunnerWithStore(pipeline);

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        Assert.AreEqual(ProcessingState.DeliveryRestriction, pipeline.State);
        Assert.IsEmpty(step.DeliveryFiles, "No delivery files may be staged when a step restricts delivery.");
        Assert.IsFalse(assetStore.Exists(jobId, "step_1_data.xtf"), "No delivery may be written to the asset store when delivery is restricted.");
        store.Verify(s => s.PipelineFinished(jobId, ProcessingState.DeliveryRestriction), Times.Once);
    }

    [TestMethod]
    public async Task PipelineDisposeFailureDoesNotAbortTheRunner()
    {
        var jobId = NewJob();
        var pipeline = BuildStubPipeline(jobId);
        pipeline.Setup(p => p.Dispose()).Throws(new IOException("The directory is not empty."));

        var (runner, store) = CreateRunnerWithStore(pipeline.Object);
        store.Setup(s => s.GetJob(jobId)).Returns(FinishedJob(jobId, Guid.NewGuid(), ProcessingState.Failed));

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        // A faulted ExecuteTask is what tears down the Parallel.ForEachAsync loop and, by default,
        // the host: the queue would stop draining for every other job as well.
        Assert.IsTrue(runner.ExecuteTask.IsCompletedSuccessfully, "A failing pipeline disposal must not fault the runner loop.");
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task UploadIsReleasedWhenPipelineDisposeFails()
    {
        var jobId = NewJob();
        var uploadId = Guid.NewGuid();
        var pipeline = BuildStubPipeline(jobId);
        pipeline.Setup(p => p.Dispose()).Throws(new IOException("The directory is not empty."));

        var (runner, store) = CreateRunnerWithStore(pipeline.Object);
        store.Setup(s => s.GetJob(jobId)).Returns(FinishedJob(jobId, uploadId, ProcessingState.Failed));

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        orchestrationServiceMock.Verify(
            c => c.ReleaseUploadAsync(uploadId),
            Times.Once,
            "The uploaded blobs of a non-deliverable run must be released even when disposing the pipeline failed.");
    }

    [TestMethod]
    public async Task ReleasingTheUploadFailingDoesNotAbortTheRunner()
    {
        var jobId = NewJob();
        var uploadId = Guid.NewGuid();
        var pipeline = BuildStubPipeline(jobId);

        var (runner, store) = CreateRunnerWithStore(pipeline.Object);
        store.Setup(s => s.GetJob(jobId)).Returns(FinishedJob(jobId, uploadId, ProcessingState.Failed));

        // Releasing the upload reaches the upload store over the network, so it fails for reasons that have
        // nothing to do with the job: an expired token, a timeout, a storage that is briefly unreachable.
        orchestrationServiceMock
            .Setup(c => c.ReleaseUploadAsync(uploadId))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable."));

        await runner.StartAsync(CancellationToken.None);
        await runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        await runner.StopAsync(CancellationToken.None);

        Assert.IsTrue(runner.ExecuteTask.IsCompletedSuccessfully, "A failing upload release must not fault the runner loop.");
        orchestrationServiceMock.Verify(c => c.ReleaseUploadAsync(uploadId), Times.Once);
    }

    [TestMethod]
    public async Task RunnerSurvivesTerminalTransitionRejectedByTheStore()
    {
        var store = new ProcessingJobStore();
        var gate = new TaskCompletionSource();
        var uploadReleased = new TaskCompletionSource();

        var job = store.CreateJob(Guid.NewGuid());
        createdJobIds.Add(job.Id);

        // Releasing the upload is the last thing the runner does for a work item, so it signals that the
        // body reached the end of its finally block rather than being torn out of it.
        orchestrationServiceMock
            .Setup(c => c.ReleaseUploadAsync(job.UploadId))
            .Callback(() => uploadReleased.TrySetResult())
            .Returns(Task.CompletedTask);

        using var pipeline = BuildPipeline(job.Id, BuildBlockingStep("step_1", gate.Task));
        store.AttachPipeline(job.Id, pipeline, 1);
        store.EnqueueForProcessing(job.Id, Array.Empty<IPipelineFile>());

        using var runner = CreateRunner(store);
        await runner.StartAsync(CancellationToken.None);
        try
        {
            // The job reaches a terminal state while its pipeline is still running, so the transition the
            // runner attempts once the pipeline finishes is no longer valid and the strict guard rejects it.
            await WaitUntilAsync(() => pipeline.State == ProcessingState.Running, TimeSpan.FromSeconds(10));
            store.MarkAsFailed(job.Id);
            gate.SetResult();

            await uploadReleased.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // The real store leaves its queue open, so a runner that survived the rejected transition just
            // keeps waiting for more work, while one that let the exception escape ends its loop right after
            // this work item. The check has to happen before StopAsync: cancelling the runner replaces an
            // already faulted loop with a cancelled one and would hide exactly what is being asserted.
            var firstToSettle = await Task.WhenAny(runner.ExecuteTask!, Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.AreNotSame(
                runner.ExecuteTask,
                firstToSettle,
                $"The runner loop ended after the rejected transition: {runner.ExecuteTask!.Exception?.InnerException?.Message}");
            Assert.AreEqual(ProcessingState.Failed, store.GetJob(job.Id)!.State, "The job keeps the state it already reached.");
        }
        finally
        {
            gate.TrySetResult();
            await runner.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// A stand-in pipeline whose run completes without steps in a state that is not deliverable, so the
    /// runner reaches its cleanup with the upload still to release.
    /// </summary>
    private static Mock<IPipeline> BuildStubPipeline(Guid jobId)
    {
        var pipeline = new Mock<IPipeline>();
        pipeline.SetupGet(p => p.Id).Returns("stub_pipeline");
        pipeline.SetupGet(p => p.JobId).Returns(jobId);
        pipeline.SetupGet(p => p.Steps).Returns(new List<IPipelineStep>());
        pipeline.SetupGet(p => p.State).Returns(ProcessingState.Failed);
        pipeline
            .Setup(p => p.Run(It.IsAny<IReadOnlyList<IPipelineFile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineContext
            {
                Upload = Array.Empty<IPipelineFile>(),
                StepResults = new Dictionary<string, StepResult>(),
            });

        return pipeline;
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

    private static ProcessingJob FinishedJob(Guid jobId, Guid uploadId, ProcessingState state)
        => new ProcessingJob(jobId, uploadId, 1, DateTime.UtcNow) { State = state };

    private (ProcessingRunner Runner, Mock<IProcessingJobStore> Store) CreateRunnerWithStore(IPipeline pipeline, TimeSpan? jobTimeout = null, IReadOnlyList<IPipelineFile>? uploads = null)
    {
        var channel = Channel.CreateUnbounded<ProcessingWorkItem>();
        channel.Writer.TryWrite(new ProcessingWorkItem(pipeline, uploads ?? Array.Empty<IPipelineFile>()));
        channel.Writer.Complete();

        var store = new Mock<IProcessingJobStore>();
        store.SetupGet(s => s.ProcessingQueue).Returns(channel.Reader);

        // Without these the mock answers false, which is the "job unknown or already terminal" case and
        // would send every test down the runner's warning branch instead of its normal one.
        store.Setup(s => s.TryMarkAsFailed(It.IsAny<Guid>())).Returns(true);
        store.Setup(s => s.TryPipelineFinished(It.IsAny<Guid>(), It.IsAny<ProcessingState>())).Returns(true);

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

    private PipelineStep BuildUploadPassthroughStep(string id) =>
        PipelineStep
            .Builder()
            .Id(id)
            .DisplayName(LocalizedText.Empty)
            .PipelineDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
            .Inputs(new Dictionary<string, InputValue> { ["files"] = new InputValue.UploadReference() })
            .OutputActions([new OutputActionConfig { Property = "Result", Actions = new HashSet<OutputAction>(new[] { OutputAction.Delivery }) }])
            .Process(new PassthroughProcess())
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

    private PipelineStep BuildWarningDeliveryStep(string id, string outputKey, string originalFileName, string content) =>
        PipelineStep
            .Builder()
            .Id(id)
            .DisplayName(LocalizedText.Empty)
            .Inputs(new Dictionary<string, InputValue>())
            .OutputActions([new OutputActionConfig { Property = "Result", Actions = new HashSet<OutputAction>(new[] { OutputAction.Delivery }) }])
            .StepConditions(new PipelineStepConditionsConfig
            {
                Post = new PipelineStepPostConditionConfig
                {
                    WarnConditions = new List<ConditionConfig> { new ConditionConfig { Expression = "1 == 1" } },
                },
            })
            .Process(new FileEmittingProcess(WriteTempFile(content), originalFileName))
            .Logger(pipelineLogger)
            .Build();

    private PipelineStep BuildRestrictDeliveryStep(string id, string outputKey, string originalFileName, string content) =>
        PipelineStep
            .Builder()
            .Id(id)
            .DisplayName(LocalizedText.Empty)
            .Inputs(new Dictionary<string, InputValue>())
            .OutputActions([new OutputActionConfig { Property = "Result", Actions = new HashSet<OutputAction>(new[] { OutputAction.Delivery }) }])
            .StepConditions(new PipelineStepConditionsConfig
            {
                Post = new PipelineStepPostConditionConfig
                {
                    RestrictDeliveryConditions = new List<ConditionConfig> { new ConditionConfig { Expression = "1 == 1" } },
                },
            })
            .Process(new FileEmittingProcess(WriteTempFile(content), originalFileName))
            .Logger(pipelineLogger)
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
