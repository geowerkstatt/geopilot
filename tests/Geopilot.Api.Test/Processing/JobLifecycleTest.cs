using Geopilot.Api.Models;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Processing;

[TestClass]
public class JobLifecycleTest
{
    private Context context;
    private string uploadRoot;
    private DirectUploadStorage uploadStorage;
    private UploadStore uploadStore;
    private ProcessingJobStore jobStore;
    private PipelineRunRecorder recorder;
    private ServiceProvider services;
    private Mandate mandate;
    private Mock<IJobCompletionHandler> completionHandler;
    private JobLifecycle lifecycle;

    // Outcome, state of the job and in the protocol, upload released, machine delivery declared, failure reason in the protocol.
    public static IEnumerable<object?[]> RunOutcomes =>
        [
            [new RunOutcome.Completed(ProcessingState.Success), ProcessingState.Success, true, true, null],
            [new RunOutcome.Completed(ProcessingState.Warning), ProcessingState.Warning, true, true, null],
            [new RunOutcome.Completed(ProcessingState.DeliveryRestriction), ProcessingState.DeliveryRestriction, true, false, null],
            [new RunOutcome.Completed(ProcessingState.Failed), ProcessingState.Failed, true, false, null],
            [new RunOutcome.Completed(ProcessingState.Cancelled), ProcessingState.Cancelled, false, false, null],
            [new RunOutcome.Completed(ProcessingState.Pending), ProcessingState.Failed, true, false, "The pipeline returned in the non-terminal state <Pending>."],
            [new RunOutcome.Completed(ProcessingState.Running), ProcessingState.Failed, true, false, "The pipeline returned in the non-terminal state <Running>."],
            [new RunOutcome.TimedOut(), ProcessingState.Cancelled, false, false, "Job timed out after 00:05:00."],
            [new RunOutcome.ShutDown(), ProcessingState.Cancelled, false, false, "host shutdown"],
            [new RunOutcome.Crashed("My Dog ate the Storage Disk"), ProcessingState.Failed, true, false, "My Dog ate the Storage Disk"],
        ];

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        uploadRoot = Path.Combine(Path.GetTempPath(), $"geopilot-lifecycle-test-{Guid.NewGuid():N}");
        uploadStorage = new DirectUploadStorage(Options.Create(new UploadDirectOptions { Directory = uploadRoot }), Mock.Of<ILogger<DirectUploadStorage>>());
        uploadStore = new UploadStore();
        jobStore = new ProcessingJobStore();

        var pipelineFactory = new Mock<IPipelineFactory>();
        pipelineFactory.Setup(f => f.GetDefinitionSnapshotJson(It.IsAny<string>())).Returns("{}");
        recorder = new PipelineRunRecorder(context, pipelineFactory.Object, uploadStorage, Mock.Of<IHttpContextAccessor>(), Mock.Of<ILogger<PipelineRunRecorder>>());

        var orchestration = new UploadOrchestrationService(
            uploadStorage, Mock.Of<IUploadScanService>(), jobStore, uploadStore, AssemblyInitialize.TestDirectoryProvider, Options.Create(new UploadOptions()), Mock.Of<ILogger<UploadOrchestrationService>>());

        // Registered as when machine delivery is enabled, so the tests see when a delivery is declared.
        completionHandler = new Mock<IJobCompletionHandler>();

        services = new ServiceCollection()
            .AddSingleton<IPipelineRunRecorder>(recorder)
            .AddSingleton<IUploadOrchestrationService>(orchestration)
            .AddSingleton(completionHandler.Object)
            .BuildServiceProvider();

        lifecycle = new JobLifecycle(
            Mock.Of<ILogger<JobLifecycle>>(), jobStore, services.GetRequiredService<IServiceScopeFactory>(), Options.Create(new ProcessingOptions { JobTimeout = TimeSpan.FromMinutes(5) }));

        mandate = new Mandate { Name = TestHelpers.Localized(nameof(JobLifecycleTest)), FileTypes = [".xtf"], PipelineId = "pipe_a" };
        context.Mandates.Add(mandate);
        context.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup()
    {
        services.Dispose();
        context.Dispose();
        if (Directory.Exists(uploadRoot))
            Directory.Delete(uploadRoot, true);
    }

    [TestMethod]
    [DynamicData(nameof(RunOutcomes))]
    public async Task RunFinishedCarriesOutTheConsequences(RunOutcome outcome, ProcessingState expectedState, bool uploadReleased, bool declared, string? expectedReason)
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync();

        await lifecycle.RunFinishedAsync(pipeline.Object, outcome, CancellationToken.None);

        var job = jobStore.GetJob(jobId)!;
        Assert.AreEqual(expectedState, job.State);
        Assert.IsNotNull(job.FinishedAt);
        AssertUploadKept(uploadId, !uploadReleased);
        completionHandler.Verify(h => h.OnJobFinishedAsync(jobId, It.IsAny<CancellationToken>()), declared ? Times.Once() : Times.Never());

        var run = await ReadRunAsync(jobId);
        Assert.AreEqual(expectedState, run.TerminalState);
        Assert.AreEqual(expectedReason, run.FailureReason);

        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task RunFinishedKeepsItsOutcomeWhenTheDeclarationThrows()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync();
        completionHandler
            .Setup(h => h.OnJobFinishedAsync(jobId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("completion exploded"));

        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);

        // A failing declaration is guarded on its own: the run stays successful and the consequences after it run.
        Assert.AreEqual(ProcessingState.Success, jobStore.GetJob(jobId)!.State);
        Assert.AreEqual(ProcessingState.Success, (await ReadRunAsync(jobId)).TerminalState);
        AssertUploadKept(uploadId, false);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task RunFinishedReleasesTheUploadWhenDisposingThePipelineFails()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync();
        pipeline.Setup(p => p.Dispose()).Throws(new IOException("The directory is not empty."));

        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Failed), CancellationToken.None);

        Assert.AreEqual(ProcessingState.Failed, (await ReadRunAsync(jobId)).TerminalState);
        AssertUploadKept(uploadId, false);
    }

    [TestMethod]
    public async Task RunFinishedStillCleansUpAJobThatAlreadyEnded()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync();
        Assert.IsTrue(jobStore.TryMarkAsFailed(jobId), "The job ends before its run reports.");

        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);

        // The rejected transition only logs: the job keeps the state it reached, and the pipeline and the upload still go.
        Assert.AreEqual(ProcessingState.Failed, jobStore.GetJob(jobId)!.State);
        AssertUploadKept(uploadId, false);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task PreflightPassedQueuesTheJobAndRecordsTheScan()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(passPreflight: false);

        await lifecycle.PreflightPassedAsync(jobId, new ScanResult(true), []);

        var job = jobStore.GetJob(jobId)!;
        Assert.AreEqual(ProcessingState.Running, job.State);
        Assert.IsNull(job.FinishedAt, "A queued job has not finished.");
        AssertUploadKept(uploadId, true);

        Assert.IsTrue(jobStore.ProcessingQueue.TryRead(out var workItem), "The job must be queued for its run.");
        Assert.AreSame(pipeline.Object, workItem.Pipeline);

        var run = await ReadRunAsync(jobId);
        Assert.AreEqual(ScanState.Clean, run.ScanState);
        Assert.IsNull(run.TerminalState);

        pipeline.Verify(p => p.Dispose(), Times.Never, "The run still needs the pipeline.");
    }

    [TestMethod]
    public async Task PreflightFailedFailsTheJobAndReleasesItsUpload()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(passPreflight: false);

        await lifecycle.PreflightFailedAsync(jobId, "IncompleteUpload: File missing.", null);

        var job = jobStore.GetJob(jobId)!;
        Assert.AreEqual(ProcessingState.Failed, job.State);
        Assert.IsNotNull(job.FinishedAt);
        AssertUploadKept(uploadId, false);

        var run = await ReadRunAsync(jobId);
        Assert.AreEqual(ProcessingState.Failed, run.TerminalState);
        Assert.AreEqual("IncompleteUpload: File missing.", run.FailureReason);

        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task PreflightFailedRecordsTheThreatItFound()
    {
        var (jobId, _, _) = await CreateJobAsync(passPreflight: false);

        await lifecycle.PreflightFailedAsync(jobId, "ThreatDetected: The uploaded files could not be processed.", new ScanResult(false, "eicar.xtf: Win.Test.EICAR_HDB-1"));

        var run = await ReadRunAsync(jobId);
        Assert.AreEqual(ScanState.ThreatDetected, run.ScanState);
        Assert.AreEqual("eicar.xtf: Win.Test.EICAR_HDB-1", run.ScanDetails);
    }

    [TestMethod]
    public async Task PreflightFailedStillCleansUpAJobThatAlreadyEnded()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(passPreflight: false);
        Assert.IsTrue(jobStore.TryMarkAsFailed(jobId), "The job moves on before the preflight reports its failure.");

        await lifecycle.PreflightFailedAsync(jobId, "IncompleteUpload: File missing.", null);

        // The rejected transition only logs: the pipeline and the upload still go.
        AssertUploadKept(uploadId, false);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task PreflightFailedTakesOverWhenQueueingTheJobFails()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => lifecycle.PreflightPassedAsync(jobId, new ScanResult(true), []));
        await lifecycle.PreflightFailedAsync(jobId, "The job could not be queued.", new ScanResult(true));

        Assert.AreEqual(ProcessingState.Failed, jobStore.GetJob(jobId)!.State);
        AssertUploadKept(uploadId, false);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task RetireRemovesTheJobAndKeepsTheUploadOfACancelledRun()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync();
        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.TimedOut(), CancellationToken.None);

        lifecycle.Retire(jobId);

        Assert.IsNull(jobStore.GetJob(jobId));
        AssertUploadKept(uploadId, true);
        pipeline.Verify(p => p.Dispose(), Times.Once, "Ending and retiring the job together dispose its pipeline once.");
    }

    [TestMethod]
    public void RetireToleratesAnUnknownJob() => lifecycle.Retire(Guid.NewGuid());

    /// <summary>
    /// Creates a job with a real upload and protocol record, the way the start does today. With
    /// <paramref name="passPreflight"/> the job goes on to Running through the lifecycle itself.
    /// </summary>
    private async Task<(Guid JobId, Guid UploadId, Mock<IPipeline> Pipeline)> CreateJobAsync(bool passPreflight = true)
    {
        var uploadId = Guid.NewGuid();
        var key = $"uploads/{uploadId}/data.xtf";
        await uploadStorage.WriteAsync(key, new MemoryStream("content"u8.ToArray()));
        var upload = uploadStore.CreateUpload(uploadId, ImmutableList.Create(new UploadedFileInfo("data.xtf", key, 7)));

        var job = jobStore.CreateJob(uploadId);
        var pipeline = new Mock<IPipeline>();
        pipeline.SetupGet(p => p.Id).Returns("pipe_a");
        pipeline.SetupGet(p => p.JobId).Returns(job.Id);
        pipeline.SetupGet(p => p.Steps).Returns(new List<IPipelineStep>());
        jobStore.AttachPipeline(job.Id, pipeline.Object, mandate.Id);
        await recorder.RecordJobStartedAsync(jobStore.GetJob(job.Id)!, mandate, null, upload);

        if (passPreflight)
            await lifecycle.PreflightPassedAsync(job.Id, new ScanResult(true), []);

        return (job.Id, uploadId, pipeline);
    }

    /// <summary>
    /// Checks both halves of an upload on their own, so a release that drops the session but leaves the files
    /// behind (or the other way round) cannot pass.
    /// </summary>
    private void AssertUploadKept(Guid uploadId, bool kept)
    {
        Assert.AreEqual(kept, uploadStore.GetUpload(uploadId) is not null, "upload session");
        Assert.AreEqual(kept, Directory.Exists(Path.Combine(uploadRoot, uploadId.ToString())), "uploaded files");
    }

    private async Task<PipelineRun> ReadRunAsync(Guid jobId)
    {
        // Otherwise EF answers from what it tracks instead of what was written.
        context.ChangeTracker.Clear();
        return await context.PipelineRuns.SingleAsync(r => r.JobId == jobId);
    }
}
