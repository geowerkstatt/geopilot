using Geopilot.Api.Exceptions;
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
using System.Threading.Channels;

namespace Geopilot.Api.Processing;

[TestClass]
public class JobLifecycleTest
{
    private const string PipelineId = "pipe_a";

    // One per test: mandate keys are unique, and the tests run in parallel.
    private readonly string mandateKey = $"LIFECYCLE-{Guid.NewGuid()}";

    private Context context;
    private string uploadRoot;
    private DirectUploadStorage uploadStorage;
    private UploadStore uploadStore;
    private ProcessingJobStore jobStore;
    private Mock<IPipelineFactory> pipelineFactory;
    private Channel<PreflightRequest> preflightQueue;
    private PipelineRunRecorder recorder;
    private ServiceProvider services;
    private Mandate mandate;
    private Declarer declarer;
    private Mock<IDeliveryDeclarationService> declarationService;
    private JobLifecycle lifecycle;

    private static readonly DeliveryFields Fields = new(PartialDelivery: null, PrecursorDeliveryId: null, Comment: "declared by geopilot");

    // What geopilot's own declaration answers, and the failure the attempt then reports, if any.
    public static IEnumerable<object?[]> DeclarationResults =>
        [
            [new DeliveryDeclarationResult(DeliveryDeclarationStatus.Created, DeliveryId: 42), null],
            [new DeliveryDeclarationResult(DeliveryDeclarationStatus.AlreadyDeclared, DeliveryId: 42, Message: "already delivered"), null],
            [new DeliveryDeclarationResult(DeliveryDeclarationStatus.JobNotDeliverable, Message: "not deliverable"), null],
            [new DeliveryDeclarationResult(DeliveryDeclarationStatus.JobNotDeclarable, Message: "Nobody can declare it."), "Nobody can declare it."],
            [new DeliveryDeclarationResult(DeliveryDeclarationStatus.NoAssets, Message: "No assets found."), "No assets found."],
            [new DeliveryDeclarationResult(DeliveryDeclarationStatus.FieldRulesViolated, FieldErrors: new Dictionary<string, string[]> { ["comment"] = ["Is required."] }), "The delivery details do not satisfy the mandate. comment: Is required."],
        ];

    // Outcome, state of the job and in the protocol, upload released, delivery declared by geopilot, failure reason in the protocol.
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

        preflightQueue = Channel.CreateUnbounded<PreflightRequest>();
        pipelineFactory = new Mock<IPipelineFactory>();
        pipelineFactory.Setup(f => f.GetDefinitionSnapshotJson(It.IsAny<string>())).Returns("{}");
        recorder = new PipelineRunRecorder(context, pipelineFactory.Object, uploadStorage, Mock.Of<IHttpContextAccessor>(), Mock.Of<ILogger<PipelineRunRecorder>>());

        var orchestration = new UploadOrchestrationService(
            uploadStorage, Mock.Of<IUploadScanService>(), jobStore, uploadStore, AssemblyInitialize.TestDirectoryProvider, Options.Create(new UploadOptions()), Mock.Of<ILogger<UploadOrchestrationService>>());

        // Mocked, so the tests see when and for whom geopilot declares a delivery; declaring is tested on its own.
        declarationService = new Mock<IDeliveryDeclarationService>();
        declarationService
            .Setup(d => d.DeclareAsync(It.IsAny<Guid>(), It.IsAny<DeliveryFields>(), It.IsAny<Declarer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryDeclarationResult(DeliveryDeclarationStatus.Created, DeliveryId: 42));

        services = new ServiceCollection()
            .AddSingleton<IPipelineRunRecorder>(recorder)
            .AddSingleton<IUploadOrchestrationService>(orchestration)
            .AddSingleton(declarationService.Object)
            .BuildServiceProvider();

        lifecycle = new JobLifecycle(
            Mock.Of<ILogger<JobLifecycle>>(),
            jobStore,
            pipelineFactory.Object,
            preflightQueue.Writer,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ProcessingOptions { JobTimeout = TimeSpan.FromMinutes(5) }));

        mandate = new Mandate { Name = TestHelpers.Localized(nameof(JobLifecycleTest)), Key = mandateKey, FileTypes = [".xtf"], PipelineId = PipelineId };
        context.Mandates.Add(mandate);
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        context.SaveChanges();
        declarer = Declarer.ForUser(user.Id);
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
    public async Task StartCreatesAPendingJobAndHandsItToThePreflight()
    {
        var upload = await CreateUploadAsync();
        var pipeline = SetupPipeline();

        var job = await StartJobAsync(upload, declarer);

        Assert.AreEqual(ProcessingState.Pending, job.State);
        Assert.AreSame(pipeline.Object, job.Pipeline);
        Assert.AreEqual(mandate.Id, job.MandateId);
        Assert.IsTrue(job.IsDeclarable);
        Assert.IsNull(job.DeliveryIntent, "Without delivery fields the delivery is declared by hand, if at all.");
        Assert.AreEqual(job, jobStore.GetJob(job.Id));

        Assert.IsTrue(preflightQueue.Reader.TryRead(out var request), "The preflight must be asked to check the job.");
        Assert.AreEqual(new PreflightRequest(job.Id, upload.Id), request);

        var run = await ReadRunAsync(job.Id);
        Assert.IsNull(run.TerminalState);
        Assert.AreEqual(declarer.UserId, run.UserId, "The protocol must name who started the job.");
        AssertUploadKept(upload.Id, true);
        pipeline.Verify(p => p.Dispose(), Times.Never, "The run still needs the pipeline.");
    }

    [TestMethod]
    public async Task StartBindsTheDeliveryIntentToTheJob()
    {
        var upload = await CreateUploadAsync();
        SetupPipeline();

        var job = await StartJobAsync(upload, declarer, deliveryFields: Fields);

        // Bound when the job is created, so no run can end before geopilot knows to declare its delivery.
        Assert.AreEqual(new DeliveryIntent(declarer, mandateKey, Fields), job.DeliveryIntent);
    }

    [TestMethod]
    public async Task StartRefusesADeliveryIntentWithoutADeclarer()
    {
        var upload = await CreateUploadAsync();
        SetupPipeline();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => StartJobAsync(upload, startedBy: null, deliveryFields: Fields));

        AssertStartLeftNoJob(upload.Id);
    }

    [TestMethod]
    public async Task StartLeavesNoJobWhenThePipelineCannotBeCreated()
    {
        var upload = await CreateUploadAsync();
        pipelineFactory
            .Setup(f => f.CreatePipeline(PipelineId, It.IsAny<Guid>()))
            .Throws(new InvalidOperationException("A processor could not be instantiated."));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => StartJobAsync(upload));

        AssertStartLeftNoJob(upload.Id);

        // The upload is still there for another start.
        SetupPipeline();
        var job = await StartJobAsync(upload);
        Assert.AreEqual(ProcessingState.Pending, job.State);
    }

    [TestMethod]
    public async Task StartLeavesNoJobWhenTheStartRecordCannotBeWritten()
    {
        var upload = await CreateUploadAsync();
        var pipeline = SetupPipeline();
        pipelineFactory
            .Setup(f => f.GetDefinitionSnapshotJson(PipelineId))
            .Throws(new InvalidOperationException("The definition snapshot could not be read."));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => StartJobAsync(upload));

        AssertStartLeftNoJob(upload.Id);
        pipeline.Verify(p => p.Dispose(), Times.Once, "The pipeline of a job that never started must not leak.");
    }

    [TestMethod]
    [DataRow(false, ProcessingState.Pending, DisplayName = "First job still in its preflight")]
    [DataRow(true, ProcessingState.Running, DisplayName = "First job running")]
    public async Task StartRefusesASecondJobForTheSameUpload(bool passPreflight, ProcessingState firstState)
    {
        var (firstId, uploadId, pipeline) = await CreateJobAsync(passPreflight);
        var upload = uploadStore.GetUpload(uploadId);
        Assert.IsNotNull(upload);
        Assert.IsTrue(preflightQueue.Reader.TryRead(out _));

        await Assert.ThrowsExactlyAsync<JobAlreadyActiveException>(() => StartJobAsync(upload));

        Assert.HasCount(1, jobStore.GetJobIds());
        Assert.AreEqual(firstState, jobStore.GetJob(firstId)!.State);
        Assert.IsFalse(preflightQueue.Reader.TryRead(out _), "The refused start must not reach the preflight.");
        pipelineFactory.Verify(f => f.CreatePipeline(PipelineId, It.IsAny<Guid>()), Times.Once);
        pipeline.Verify(p => p.Dispose(), Times.Never, "The refusal must leave the active job alone.");
    }

    [TestMethod]
    public async Task StartAcceptsTheUploadAgainAfterACancelledRun()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync();
        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.TimedOut(), CancellationToken.None);
        var upload = uploadStore.GetUpload(uploadId);
        Assert.IsNotNull(upload, "A cancelled run keeps its upload for another start.");

        SetupPipeline();
        var second = await StartJobAsync(upload);

        Assert.AreNotEqual(jobId, second.Id);
        Assert.AreEqual(ProcessingState.Pending, second.State);
        Assert.AreEqual(ProcessingState.Cancelled, jobStore.GetJob(jobId)!.State);
    }

    [TestMethod]
    [DynamicData(nameof(RunOutcomes))]
    public async Task RunFinishedCarriesOutTheConsequences(RunOutcome outcome, ProcessingState expectedState, bool uploadReleased, bool declared, string? expectedReason)
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(withDeliveryIntent: true);

        await lifecycle.RunFinishedAsync(pipeline.Object, outcome, CancellationToken.None);

        var job = jobStore.GetJob(jobId)!;
        Assert.AreEqual(expectedState, job.State);
        Assert.IsNotNull(job.FinishedAt);
        AssertUploadKept(uploadId, !uploadReleased);
        declarationService.Verify(d => d.DeclareAsync(jobId, Fields, declarer, It.IsAny<CancellationToken>()), declared ? Times.Once() : Times.Never());

        var run = await ReadRunAsync(jobId);
        Assert.AreEqual(expectedState, run.TerminalState);
        Assert.AreEqual(expectedReason, run.FailureReason);

        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task RunFinishedKeepsItsOutcomeWhenTheDeclarationThrows()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(withDeliveryIntent: true);
        declarationService
            .Setup(d => d.DeclareAsync(jobId, Fields, declarer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The database went away."));

        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);

        // A failing declaration is guarded on its own: the run stays successful and the consequences after it run.
        // The attempt learns of it, instead of reporting a delivery in progress until the job is retired.
        var job = jobStore.GetJob(jobId)!;
        Assert.AreEqual(ProcessingState.Success, job.State);
        Assert.AreEqual("The delivery could not be declared because of an unexpected error.", job.DeclarationFailure);
        Assert.AreEqual(ProcessingState.Success, (await ReadRunAsync(jobId)).TerminalState);
        AssertUploadKept(uploadId, false);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(DeclarationResults))]
    public async Task RunFinishedRecordsWhyTheDeliveryCouldNotBeDeclared(DeliveryDeclarationResult result, string? expectedFailure)
    {
        var (jobId, _, pipeline) = await CreateJobAsync(withDeliveryIntent: true);
        declarationService
            .Setup(d => d.DeclareAsync(jobId, Fields, declarer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);

        // A created or adopted delivery is its own record, and data that did not pass is a rejected attempt, not a
        // failed declaration. Every other refusal is the one thing the caller of the attempt gets to read.
        Assert.AreEqual(expectedFailure, jobStore.GetJob(jobId)!.DeclarationFailure);
    }

    [TestMethod]
    public async Task RunFinishedDeclaresNothingWithoutADeliveryIntent()
    {
        var (_, _, pipeline) = await CreateJobAsync();

        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);

        declarationService.Verify(
            d => d.DeclareAsync(It.IsAny<Guid>(), It.IsAny<DeliveryFields>(), It.IsAny<Declarer>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "A run started from the web interface is declared by hand, if at all.");
    }

    [TestMethod]
    public async Task RunFinishedReleasesTheUploadOfARunNobodyCanDeclare()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(isDeclarable: false);

        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);

        // Nothing was staged for it, so its upload goes at the end of the run like that of any deliverable one.
        Assert.AreEqual(ProcessingState.Success, jobStore.GetJob(jobId)!.State);
        AssertUploadKept(uploadId, false);
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
    /// Starts a job for a real upload through the lifecycle itself. With <paramref name="passPreflight"/> the job
    /// goes on to Running as well.
    /// </summary>
    private async Task<(Guid JobId, Guid UploadId, Mock<IPipeline> Pipeline)> CreateJobAsync(bool passPreflight = true, bool isDeclarable = true, bool withDeliveryIntent = false)
    {
        var upload = await CreateUploadAsync();
        var pipeline = SetupPipeline();
        var job = withDeliveryIntent
            ? await StartJobAsync(upload, declarer, isDeclarable, Fields)
            : await StartJobAsync(upload, isDeclarable: isDeclarable);

        if (passPreflight)
            await lifecycle.PreflightPassedAsync(job.Id, new ScanResult(true), []);

        return (job.Id, upload.Id, pipeline);
    }

    private Task<ProcessingJob> StartJobAsync(UploadInfo upload, Declarer? startedBy = null, bool isDeclarable = true, DeliveryFields? deliveryFields = null)
        => lifecycle.StartAsync(upload, mandate, PipelineId, startedBy, isDeclarable, deliveryFields);

    private async Task<UploadInfo> CreateUploadAsync()
    {
        var uploadId = Guid.NewGuid();
        var key = $"uploads/{uploadId}/data.xtf";
        await uploadStorage.WriteAsync(key, new MemoryStream("content"u8.ToArray()));
        return uploadStore.CreateUpload(uploadId, ImmutableList.Create(new UploadedFileInfo("data.xtf", key, 7)));
    }

    /// <summary>
    /// Lets the pipeline factory hand out a pipeline mock, which learns its job id the moment it is created.
    /// </summary>
    private Mock<IPipeline> SetupPipeline()
    {
        var pipeline = new Mock<IPipeline>();
        pipeline.SetupGet(p => p.Id).Returns(PipelineId);
        pipeline.SetupGet(p => p.Steps).Returns(new List<IPipelineStep>());
        pipelineFactory
            .Setup(f => f.CreatePipeline(PipelineId, It.IsAny<Guid>()))
            .Returns((string _, Guid jobId) =>
            {
                pipeline.SetupGet(p => p.JobId).Returns(jobId);
                return pipeline.Object;
            });

        return pipeline;
    }

    private void AssertStartLeftNoJob(Guid uploadId)
    {
        Assert.IsEmpty(jobStore.GetJobIds(), "A failed start must not leave a job behind.");
        Assert.IsFalse(preflightQueue.Reader.TryRead(out _), "A job that never started must not reach the preflight.");
        AssertUploadKept(uploadId, true);
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
