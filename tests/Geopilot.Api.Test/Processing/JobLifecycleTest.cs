using Geopilot.Api.Exceptions;
using Geopilot.Api.Models;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using System.Collections.Immutable;
using System.Data.Common;
using System.Text.Json;
using System.Threading.Channels;

namespace Geopilot.Api.Processing;

[TestClass]
public class JobLifecycleTest
{
    public interface IPipelineFactoryDouble : IPipelineFactory
    {
        new IReadOnlySet<Type> GetUsedBindingTypes(string pipelineId);

        /// <summary>
        /// Implements the internal <see cref="IPipelineFactory.GetUsedBindingTypes"/> method using the public <see cref="IPipelineFactoryDouble.GetUsedBindingTypes"/>
        /// above to enable mock setups without exposing internal members to the generated assembly of Moq.
        /// </summary>
        IReadOnlySet<Type> IPipelineFactory.GetUsedBindingTypes(string pipelineId) => GetUsedBindingTypes(pipelineId);
    }

    private const string PipelineId = "pipe_a";

    // One per test: mandate keys are unique, and the tests run in parallel.
    private readonly string mandateKey = $"LIFECYCLE-{Guid.NewGuid()}";

    private FlakyDatabase database;
    private Context context;
    private string uploadRoot;
    private DirectUploadStorage uploadStorage;
    private UploadStore uploadStore;
    private ProcessingJobStore jobStore;
    private Mock<IPipelineFactoryDouble> pipelineFactory;
    private Channel<PreflightRequest> preflightQueue;
    private Recorder recorder;
    private ServiceProvider services;
    private Mandate mandate;
    private Supplier supplier;
    private Mock<IDeliveryCreationService> creationService;
    private JobLifecycle lifecycle;

    private static readonly DeliveryFields Fields = new(PartialDelivery: null, PrecursorDeliveryId: null, Comment: "created by geopilot");

    // What geopilot's own creation answers, and the failure the attempt then reports, if any.
    public static IEnumerable<object?[]> CreationResults =>
        [
            [new DeliveryCreationResult(DeliveryCreationStatus.Created, DeliveryId: 42), null],
            [new DeliveryCreationResult(DeliveryCreationStatus.AlreadyDelivered, DeliveryId: 42, Message: "already delivered"), null],
            [new DeliveryCreationResult(DeliveryCreationStatus.JobNotDeliverable, Message: "not deliverable"), null],
            [new DeliveryCreationResult(DeliveryCreationStatus.JobNotPermitted, Message: "Nobody may deliver it."), "Nobody may deliver it."],
            [new DeliveryCreationResult(DeliveryCreationStatus.NoAssets, Message: "No assets found."), "No assets found."],
            [new DeliveryCreationResult(DeliveryCreationStatus.FieldRulesViolated, FieldErrors: new Dictionary<string, string[]> { ["comment"] = ["Is required."] }), "The delivery details do not satisfy the mandate. comment: Is required."],
        ];

    // Outcome, state of the job and in the protocol, upload released, delivery created by geopilot, failure reason in the protocol.
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
            [new RunOutcome.Interrupted("The protocol could not record it."), ProcessingState.Cancelled, false, false, "The protocol could not record it."],
            [new RunOutcome.Crashed("My Dog ate the Storage Disk"), ProcessingState.Failed, true, false, "My Dog ate the Storage Disk"],
        ];

    // How a test ends a job that is held, from its pipeline or its preflight.
    public enum JobEnd
    {
        RunSucceeded,
        PreflightFailed,
        PreflightInterrupted,
    }

    [TestInitialize]
    public void Initialize()
    {
        database = new FlakyDatabase();
        context = AssemblyInitialize.DbFixture.GetTestContext(database);
        uploadRoot = Path.Combine(Path.GetTempPath(), $"geopilot-lifecycle-test-{Guid.NewGuid():N}");
        uploadStorage = new DirectUploadStorage(Options.Create(new UploadDirectOptions { Directory = uploadRoot }), Mock.Of<ILogger<DirectUploadStorage>>());
        uploadStore = new UploadStore();
        jobStore = new ProcessingJobStore();

        preflightQueue = Channel.CreateUnbounded<PreflightRequest>();
        pipelineFactory = new Mock<IPipelineFactoryDouble>();
        pipelineFactory.Setup(f => f.GetDefinitionSnapshotJson(It.IsAny<string>())).Returns("{}");
        recorder = new Recorder(context, pipelineFactory.Object, uploadStorage, Mock.Of<IHttpContextAccessor>(), Mock.Of<ILogger<Recorder>>());

        var orchestration = new UploadOrchestrationService(
            uploadStorage, Mock.Of<IUploadScanService>(), jobStore, uploadStore, AssemblyInitialize.TestDirectoryProvider, Options.Create(new UploadOptions()), Mock.Of<ILogger<UploadOrchestrationService>>());

        // Mocked, so the tests see when and for whom geopilot creates a delivery; creating is tested on its own.
        creationService = new Mock<IDeliveryCreationService>();
        creationService
            .Setup(d => d.CreateAsync(It.IsAny<Guid>(), It.IsAny<DeliveryFields>(), It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryCreationResult(DeliveryCreationStatus.Created, DeliveryId: 42));

        services = new ServiceCollection()
            .AddSingleton(context)
            .AddSingleton<IRecorder>(recorder)
            .AddSingleton<IUploadOrchestrationService>(orchestration)
            .AddSingleton(creationService.Object)
            .BuildServiceProvider();

        lifecycle = CreateLifecycle(refMappingEnabled: false);

        mandate = new Mandate { Name = TestHelpers.Localized(nameof(JobLifecycleTest)), Key = mandateKey, FileTypes = [".xtf"], PipelineId = PipelineId };
        context.Mandates.Add(mandate);
        var user = context.Users.Add(new User { AuthIdentifier = Guid.NewGuid().ToString() }).Entity;
        context.SaveChanges();
        supplier = Supplier.ForUser(user.Id);
    }

    [TestCleanup]
    public void Cleanup()
    {
        services.Dispose();
        context.Dispose();
        if (Directory.Exists(uploadRoot))
            Directory.Delete(uploadRoot, true);
    }

    /// <summary>
    /// Stands in for a database that goes away for some statements: every command <see cref="Fails"/> picks fails before
    /// it reaches the database, as on a lost connection, so the transaction of the test stays usable.
    /// </summary>
    private sealed class FlakyDatabase : DbCommandInterceptor
    {
        public Func<string, bool>? Fails { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            FailIfPicked(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            FailIfPicked(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void FailIfPicked(DbCommand command)
        {
            if (Fails?.Invoke(command.CommandText) == true)
                throw new NpgsqlException("The connection to the database was lost.", new TimeoutException());
        }
    }

    [TestMethod]
    public async Task StartCreatesAPendingJobAndHandsItToThePreflight()
    {
        var upload = await CreateUploadAsync();
        var pipeline = SetupPipeline();

        var job = await StartJobAsync(upload, supplier);

        Assert.AreEqual(ProcessingState.Pending, job.State);
        Assert.AreSame(pipeline.Object, job.Pipeline);
        Assert.AreEqual(mandate.Id, job.MandateId);
        Assert.IsNull(job.DeliveryIntent, "Without delivery fields the delivery is created by hand, if at all.");
        Assert.AreEqual(job, jobStore.GetJob(job.Id));

        Assert.IsTrue(preflightQueue.Reader.TryRead(out var request), "The preflight must be asked to check the job.");
        Assert.AreEqual(new PreflightRequest(job.Id, upload.Id), request);

        var run = await ReadRunAsync(job.Id);
        Assert.IsNull(run.TerminalState);
        Assert.AreEqual(supplier.UserId, run.UserId, "The protocol must name who started the job.");
        AssertUploadKept(upload.Id, true);
        pipeline.Verify(p => p.Dispose(), Times.Never, "The run still needs the pipeline.");
    }

    [TestMethod]
    [DataRow(true, PermissionToDeliver.Permitted)]
    [DataRow(false, PermissionToDeliver.NotPermitted)]
    public async Task StartRecordsWhetherTheStarterMayDeliver(bool mayDeliver, PermissionToDeliver expectedPermission)
    {
        var upload = await CreateUploadAsync();
        SetupPipeline();

        var job = await StartJobAsync(upload, supplier, mayDeliver);

        // Both values, because a value fixed on the way to the job would pass with true alone.
        Assert.AreEqual(mayDeliver, job.MayDeliver);
        Assert.AreEqual(mayDeliver, jobStore.GetJob(job.Id)!.MayDeliver);
        Assert.AreEqual(expectedPermission, (await ReadRunAsync(job.Id)).PermissionToDeliver, "The protocol keeps it beyond the job's retention and a restart.");
    }

    [TestMethod]
    public async Task StartBindsTheDeliveryIntentToTheJob()
    {
        var upload = await CreateUploadAsync();
        SetupPipeline();

        var job = await StartJobAsync(upload, supplier, deliveryFields: Fields);

        // Bound when the job is created, so no run can end before geopilot knows to create its delivery.
        Assert.AreEqual(new DeliveryIntent(supplier, mandateKey, Fields), job.DeliveryIntent);
    }

    [TestMethod]
    public async Task StartRefusesADeliveryIntentWithoutASupplier()
    {
        var upload = await CreateUploadAsync();
        SetupPipeline();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => StartJobAsync(upload, startedBy: null, deliveryFields: Fields));

        AssertStartLeftNoJob(upload.Id);
    }

    [TestMethod]
    public async Task ReadJobBindingsBindsNoRefMappingWithoutTheSwitch()
    {
        await AddRefMappingAsync();

        var bindings = await lifecycle.ReadJobBindingsAsync(services, new HashSet<Type> { typeof(IRefMappings) });

        Assert.IsNull(bindings.Get(typeof(IRefMappings)), "Without the switch the processes do not get bound ref mappings.");
    }

    [TestMethod]
    public async Task ReadJobBindingsBindsNoRefMappingWhenUnused()
    {
        await AddRefMappingAsync();
        lifecycle = CreateLifecycle(refMappingEnabled: true);

        var bindings = await lifecycle.ReadJobBindingsAsync(services, new HashSet<Type>());

        Assert.IsNull(bindings.Get(typeof(IRefMappings)), "Without a step reading the ref mappings, no ref mappings are bound.");
    }

    [TestMethod]
    public async Task ReadJobBindingsBindsEveryRefMappingEntryWithTheSwitch()
    {
        var entry = await AddRefMappingAsync();
        lifecycle = CreateLifecycle(refMappingEnabled: true);

        var bindings = await lifecycle.ReadJobBindingsAsync(services, new HashSet<Type> { typeof(IRefMappings) });

        var refMappings = Assert.IsInstanceOfType<IRefMappings>(bindings.Get(typeof(IRefMappings)));

        // Filtered by scope: the test database is shared with tests running in parallel.
        var mapping = Assert.ContainsSingle(refMappings.Entries.Where(e => e.Scope == entry.Scope));
        Assert.AreEqual(entry.Scope, mapping.Scope);
        CollectionAssert.AreEquivalent(entry.RefDataUris, mapping.RefDataUris.ToList());
        Assert.AreEqual(entry.Comment, mapping.Comment);
    }

    [TestMethod]
    public async Task StartRecordsTheBoundRefMappingInTheProtocol()
    {
        var entry = await AddRefMappingAsync();
        var upload = await CreateUploadAsync();
        SetupPipeline();
        lifecycle = CreateLifecycle(refMappingEnabled: true);
        pipelineFactory.Setup(f => f.GetUsedBindingTypes(It.IsAny<string>())).Returns(new HashSet<Type> { typeof(IRefMappings) });

        var job = await StartJobAsync(upload);

        var run = await ReadRunAsync(job.Id);
        Assert.IsNotNull(run.Bindings, "With the switch the protocol keeps what the job was bound.");
        using var bindings = JsonDocument.Parse(run.Bindings);

        // Filtered by scope: the test database is shared with tests running in parallel.
        var recorded = Assert.ContainsSingle(bindings.RootElement.GetProperty("refMapping").EnumerateArray()
            .Where(e => e.GetProperty("scope").GetString() == entry.Scope));
        CollectionAssert.AreEqual(entry.RefDataUris, recorded.GetProperty("refDataUris").EnumerateArray().Select(uri => uri.GetString()).ToList());
        Assert.AreEqual(entry.Comment, recorded.GetProperty("comment").GetString());
    }

    [TestMethod]
    public async Task StartRecordsNoBindingsWithoutTheSwitch()
    {
        await AddRefMappingAsync();
        var upload = await CreateUploadAsync();
        SetupPipeline();

        var job = await StartJobAsync(upload);

        Assert.IsNull((await ReadRunAsync(job.Id)).Bindings, "Without the switch the host binds nothing, so the protocol has no refMapping key.");
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
    public async Task RunFinishedCarriesOutTheConsequences(RunOutcome outcome, ProcessingState expectedState, bool uploadReleased, bool created, string? expectedReason)
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(withDeliveryIntent: true);

        await lifecycle.RunFinishedAsync(pipeline.Object, outcome, CancellationToken.None);

        var job = jobStore.GetJob(jobId)!;
        Assert.AreEqual(expectedState, job.State);
        Assert.IsNotNull(job.FinishedAt);
        AssertUploadKept(uploadId, !uploadReleased);
        creationService.Verify(d => d.CreateAsync(jobId, Fields, supplier, It.IsAny<CancellationToken>()), created ? Times.Once() : Times.Never());

        var run = await ReadRunAsync(jobId);
        Assert.AreEqual(expectedState, run.TerminalState);
        Assert.AreEqual(expectedReason, run.FailureReason);

        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task RunFinishedKeepsItsOutcomeWhenTheCreationThrows()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(withDeliveryIntent: true);
        creationService
            .Setup(d => d.CreateAsync(jobId, Fields, supplier, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The database went away."));

        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);

        // A failing creation is guarded on its own: the run stays successful and the consequences after it run.
        // The attempt learns of it, instead of reporting a delivery in progress until the job is retired.
        var job = jobStore.GetJob(jobId)!;
        Assert.AreEqual(ProcessingState.Success, job.State);
        Assert.AreEqual("The delivery could not be created because of an unexpected error.", job.DeliveryFailure);
        var run = await ReadRunAsync(jobId);
        Assert.AreEqual(ProcessingState.Success, run.TerminalState);
        Assert.AreEqual("The delivery could not be created because of an unexpected error.", run.DeliveryFailure);
        AssertUploadKept(uploadId, false);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    [DynamicData(nameof(CreationResults))]
    public async Task RunFinishedRecordsWhyTheDeliveryFailed(DeliveryCreationResult result, string? expectedFailure)
    {
        var (jobId, _, pipeline) = await CreateJobAsync(withDeliveryIntent: true);
        creationService
            .Setup(d => d.CreateAsync(jobId, Fields, supplier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);

        // A created or adopted delivery is its own record, and data that did not pass is a rejected attempt, not a
        // failed creation. Every other refusal is the one thing the caller of the attempt gets to read, and the
        // protocol keeps it beyond the job's retention.
        Assert.AreEqual(expectedFailure, jobStore.GetJob(jobId)!.DeliveryFailure);
        Assert.AreEqual(expectedFailure, (await ReadRunAsync(jobId)).DeliveryFailure);
    }

    [TestMethod]
    public async Task RunFinishedCreatesNothingWithoutADeliveryIntent()
    {
        var (_, _, pipeline) = await CreateJobAsync();

        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);

        creationService.Verify(
            d => d.CreateAsync(It.IsAny<Guid>(), It.IsAny<DeliveryFields>(), It.IsAny<Supplier>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "A run started from the web interface is created by hand, if at all.");
    }

    [TestMethod]
    public async Task RunFinishedReleasesTheUploadOfARunNobodyMayDeliver()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(mayDeliver: false);

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
    public async Task PreflightInterruptedCancelsTheJobAndKeepsItsUpload()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(passPreflight: false);

        await lifecycle.PreflightInterruptedAsync(jobId, "The protocol could not record it.");

        var job = jobStore.GetJob(jobId)!;
        Assert.AreEqual(ProcessingState.Cancelled, job.State);
        Assert.IsNotNull(job.FinishedAt);
        AssertUploadKept(uploadId, true);

        var run = await ReadRunAsync(jobId);
        Assert.AreEqual(ProcessingState.Cancelled, run.TerminalState);
        Assert.AreEqual("The protocol could not record it.", run.FailureReason);

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
    public async Task RunFinishedRecordsTheTerminalStateBeforeCreatingTheDelivery()
    {
        var (jobId, _, pipeline) = await CreateJobAsync(withDeliveryIntent: true);
        ProcessingState? terminalStateWhenCreating = null;
        creationService
            .Setup(d => d.CreateAsync(jobId, Fields, supplier, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                terminalStateWhenCreating = (await ReadRunAsync(jobId)).TerminalState;
                return new DeliveryCreationResult(DeliveryCreationStatus.Created, DeliveryId: 42);
            });

        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);

        creationService.Verify(d => d.CreateAsync(jobId, Fields, supplier, It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(ProcessingState.Success, terminalStateWhenCreating, "The delivery gate reads the terminal state, so it must be written first.");
    }

    [TestMethod]
    public async Task JobIsHeldUntilItsConsequencesAreCarriedOut()
    {
        var (finishedId, _, finishedPipeline) = await CreateJobAsync();
        var (failedId, _, _) = await CreateJobAsync(passPreflight: false);
        var (interruptedId, _, _) = await CreateJobAsync(passPreflight: false);
        CollectionAssert.IsSubsetOf(new[] { finishedId, failedId, interruptedId }, lifecycle.HeldJobIds.ToArray());

        await lifecycle.RunFinishedAsync(finishedPipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);
        Assert.DoesNotContain(finishedId, lifecycle.HeldJobIds);

        await lifecycle.PreflightFailedAsync(failedId, "IncompleteUpload: File missing.", null);
        Assert.DoesNotContain(failedId, lifecycle.HeldJobIds);

        await lifecycle.PreflightInterruptedAsync(interruptedId, "The protocol could not record it.");
        Assert.IsEmpty(lifecycle.HeldJobIds);

        var upload = await CreateUploadAsync();
        SetupPipeline();
        pipelineFactory
            .Setup(f => f.GetDefinitionSnapshotJson(PipelineId))
            .Throws(new InvalidOperationException("The definition snapshot could not be read."));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => StartJobAsync(upload));
        Assert.IsEmpty(lifecycle.HeldJobIds, "A start that failed holds no job.");
    }

    [TestMethod]
    public async Task StartHoldsTheJobOnlyOnceItsStartIsRecorded()
    {
        var upload = await CreateUploadAsync();
        SetupPipeline();
        IReadOnlyCollection<Guid>? heldWhileRecording = null;
        pipelineFactory
            .Setup(f => f.GetDefinitionSnapshotJson(PipelineId))
            .Returns(() =>
            {
                heldWhileRecording = lifecycle.HeldJobIds;
                return "{}";
            });

        var job = await StartJobAsync(upload);

        // A held job without its run in the database would look dead to the heartbeat, which would abort it.
        Assert.IsNotNull(heldWhileRecording, "The recorder reads the definition while it writes the start record.");
        Assert.IsEmpty(heldWhileRecording);
        Assert.Contains(job.Id, lifecycle.HeldJobIds);
    }

    [TestMethod]
    public async Task AbortedJobKeepsItsUploadWhateverItsOutcome()
    {
        var (finishedId, finishedUploadId, finishedPipeline) = await CreateJobAsync();
        var (failedId, failedUploadId, _) = await CreateJobAsync(passPreflight: false);

        // The abort comes after the outcome was decided, while its consequences are carried out.
        lifecycle.Abort([finishedId, failedId], "heartbeat lost");
        await lifecycle.RunFinishedAsync(finishedPipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);
        await lifecycle.PreflightFailedAsync(failedId, "IncompleteUpload: File missing.", null);

        // Another instance may already process the same upload again.
        AssertUploadKept(finishedUploadId, true);
        AssertUploadKept(failedUploadId, true);
    }

    [TestMethod]
    public async Task AbortedAttemptGetsNoDeliveryAndKeepsItsUpload()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(withDeliveryIntent: true);

        // The abort comes after the run ended, before its delivery is created.
        lifecycle.Abort([jobId], "heartbeat lost");
        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);

        // Another instance may already process the same upload again; without a delivery the attempt reads as lost.
        creationService.Verify(d => d.CreateAsync(It.IsAny<Guid>(), It.IsAny<DeliveryFields>(), It.IsAny<Supplier>(), It.IsAny<CancellationToken>()), Times.Never);
        AssertUploadKept(uploadId, true);
    }

    [TestMethod]
    [DataRow(JobEnd.RunSucceeded)]
    [DataRow(JobEnd.PreflightFailed)]
    [DataRow(JobEnd.PreflightInterrupted)]
    public async Task EndOfARunTheDatabaseConsidersDeadLeavesItDead(JobEnd end)
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(passPreflight: end == JobEnd.RunSucceeded, withDeliveryIntent: true);
        var abortToken = lifecycle.AbortTokenOf(jobId);

        // The instance stood still for longer than Liveness.DeadAfter, as in a frozen container, and wakes up to the end.
        await SetHeartbeatAgeAsync(jobId, TimeSpan.FromMinutes(4));
        await EndAsync(jobId, pipeline, end);

        // Another instance may already process the same upload again: the run stays dead, and the job ends aborted.
        Assert.IsNull((await ReadRunAsync(jobId)).TerminalState);
        Assert.IsFalse(await IsAnswerableAsync(jobId), "The status of a dead run stays 404.");
        creationService.Verify(d => d.CreateAsync(It.IsAny<Guid>(), It.IsAny<DeliveryFields>(), It.IsAny<Supplier>(), It.IsAny<CancellationToken>()), Times.Never);
        AssertUploadKept(uploadId, true);
        Assert.IsTrue(abortToken.IsCancellationRequested, "The job is aborted.");
        Assert.AreEqual(ProcessingState.Cancelled, jobStore.GetJob(jobId)!.State);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task EndOfARunThatIsStillAliveIsCarriedOut()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(withDeliveryIntent: true);

        // The last beat came long before the end, yet within Liveness.DeadAfter.
        await SetHeartbeatAgeAsync(jobId, TimeSpan.FromMinutes(2));
        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);

        Assert.IsTrue(await HeartbeatIsNowAsync(jobId), "The end confirms the run with one more heartbeat.");
        Assert.AreEqual(ProcessingState.Success, (await ReadRunAsync(jobId)).TerminalState);
        Assert.AreEqual(ProcessingState.Success, jobStore.GetJob(jobId)!.State);
        creationService.Verify(d => d.CreateAsync(jobId, Fields, supplier, It.IsAny<CancellationToken>()), Times.Once);
        AssertUploadKept(uploadId, false);
    }

    [TestMethod]
    public async Task RunFinishedKeepsTheUploadWhenItsEndCannotBeRecorded()
    {
        var (jobId, uploadId, pipeline) = await CreateJobAsync(withDeliveryIntent: true);

        // The database goes away right after the run was confirmed, through every attempt of the terminal write.
        database.Fails = sql => sql.StartsWith("UPDATE", StringComparison.Ordinal) && sql.Contains("\"TerminalState\"", StringComparison.Ordinal);
        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None);
        database.Fails = null;

        // The confirmation passed, so the job ended as it ran; a run found dead would have been cancelled.
        Assert.AreEqual(ProcessingState.Success, jobStore.GetJob(jobId)?.State);

        // Without its terminal state the run reads dead three minutes later, and its upload can be started again.
        Assert.IsNull((await ReadRunAsync(jobId)).TerminalState);
        creationService.Verify(d => d.CreateAsync(It.IsAny<Guid>(), It.IsAny<DeliveryFields>(), It.IsAny<Supplier>(), It.IsAny<CancellationToken>()), Times.Never);
        AssertUploadKept(uploadId, true);
    }

    [TestMethod]
    public async Task AbortCancelsTheTokenOfAHeldJobAndRemembersTheFirstReason()
    {
        var (jobId, _, pipeline) = await CreateJobAsync();
        var otherId = Guid.NewGuid();
        var token = lifecycle.AbortTokenOf(jobId);
        Assert.IsFalse(token.IsCancellationRequested);
        Assert.IsNull(lifecycle.AbortReasonOf(jobId));

        lifecycle.Abort([jobId, otherId], "first");
        lifecycle.Abort([jobId], "second");

        Assert.IsTrue(token.IsCancellationRequested);
        Assert.AreEqual("first", lifecycle.AbortReasonOf(jobId));
        Assert.IsNull(lifecycle.AbortReasonOf(otherId), "A job that is not held is ignored.");
        Assert.AreEqual(CancellationToken.None, lifecycle.AbortTokenOf(otherId));

        await lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Interrupted("first"), CancellationToken.None);

        Assert.AreEqual(CancellationToken.None, lifecycle.AbortTokenOf(jobId));
        Assert.IsNull(lifecycle.AbortReasonOf(jobId));
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
    private async Task<(Guid JobId, Guid UploadId, Mock<IPipeline> Pipeline)> CreateJobAsync(bool passPreflight = true, bool mayDeliver = true, bool withDeliveryIntent = false)
    {
        var upload = await CreateUploadAsync();
        var pipeline = SetupPipeline();
        var job = withDeliveryIntent
            ? await StartJobAsync(upload, supplier, mayDeliver, Fields)
            : await StartJobAsync(upload, mayDeliver: mayDeliver);

        if (passPreflight)
            await lifecycle.PreflightPassedAsync(job.Id, new ScanResult(true), []);

        return (job.Id, upload.Id, pipeline);
    }

    private JobLifecycle CreateLifecycle(bool refMappingEnabled) => new(
        Mock.Of<ILogger<JobLifecycle>>(),
        jobStore,
        pipelineFactory.Object,
        preflightQueue.Writer,
        services.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new ProcessingOptions { JobTimeout = TimeSpan.FromMinutes(5) }),
        Options.Create(new RefMappingOptions { Enabled = refMappingEnabled }));

    private Task<ProcessingJob> StartJobAsync(UploadInfo upload, Supplier? startedBy = null, bool mayDeliver = true, DeliveryFields? deliveryFields = null)
        => lifecycle.StartAsync(upload, mandate, PipelineId, startedBy, mayDeliver, deliveryFields);

    private async Task<UploadInfo> CreateUploadAsync()
    {
        var uploadId = Guid.NewGuid();
        var key = $"uploads/{uploadId}/data.xtf";
        await uploadStorage.WriteAsync(key, new MemoryStream("content"u8.ToArray()));
        return uploadStore.CreateUpload(uploadId, ImmutableList.Create(new UploadedFileInfo("data.xtf", key, 7)));
    }

    /// <summary>
    /// Lets the pipeline factory hand out a pipeline mock, which learns its job id the moment it is created. Set up
    /// on the public overload: Moq cannot see the internal one with the bindings, whose default body forwards here.
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

    private async Task<Recording> ReadRunAsync(Guid jobId)
    {
        // Otherwise EF answers from what it tracks instead of what was written.
        context.ChangeTracker.Clear();
        return await context.Recordings.SingleAsync(r => r.JobId == jobId);
    }

    private Task EndAsync(Guid jobId, Mock<IPipeline> pipeline, JobEnd end) => end switch
    {
        JobEnd.RunSucceeded => lifecycle.RunFinishedAsync(pipeline.Object, new RunOutcome.Completed(ProcessingState.Success), CancellationToken.None),
        JobEnd.PreflightFailed => lifecycle.PreflightFailedAsync(jobId, "IncompleteUpload: File missing.", new ScanResult(true)),
        JobEnd.PreflightInterrupted => lifecycle.PreflightInterruptedAsync(jobId, "The protocol could not record it."),
        _ => throw new ArgumentOutOfRangeException(nameof(end), end, null),
    };

    // Moves the last heartbeat back by the database clock, which stands still within the transaction of a test.
    private async Task SetHeartbeatAgeAsync(Guid jobId, TimeSpan age) =>
        await context.Recordings
            .Where(r => r.JobId == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.HeartbeatAt, r => DateTime.UtcNow - age));

    private Task<bool> HeartbeatIsNowAsync(Guid jobId) =>
        context.Recordings.AnyAsync(r => r.JobId == jobId && r.HeartbeatAt == DateTime.UtcNow);

    private Task<bool> IsAnswerableAsync(Guid jobId) =>
        context.Recordings.WhereAnswerable(context.Deliveries.IgnoreQueryFilters(), TimeSpan.FromDays(1)).AnyAsync(r => r.JobId == jobId);

    private async Task<RefMapping> AddRefMappingAsync()
    {
        var entry = new RefMapping() { Scope = Guid.NewGuid().ToString(), RefDataUris = ["ilidata:dmav_LFP3_449", "ilidata:dmav_HFP3_449"], Comment = "Sauge" };
        context.RefMappings.Add(entry);
        await context.SaveChangesAsync();
        return entry;
    }
}
