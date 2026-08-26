using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Test.Processing;

[TestClass]
public class PipelineRunRecorderTest
{
    private Context context;
    private Mock<IPipelineFactory> pipelineFactoryMock;
    private Mock<IUploadStorage> uploadStorageMock;
    private Mock<IHttpContextAccessor> httpContextAccessorMock;
    private HttpContext? currentHttpContext;
    private PipelineRunRecorder recorder;
    private Mandate mandate;
    private User user;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();

        pipelineFactoryMock = new Mock<IPipelineFactory>();
        pipelineFactoryMock.Setup(f => f.GetDefinitionSnapshotJson(It.IsAny<string>())).Returns("{}");

        uploadStorageMock = new Mock<IUploadStorage>();
        uploadStorageMock.SetupGet(s => s.StorageLocation).Returns("https://storage.example.com/uploads");

        httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.SetupGet(a => a.HttpContext).Returns(() => currentHttpContext);

        recorder = new PipelineRunRecorder(
            context,
            pipelineFactoryMock.Object,
            uploadStorageMock.Object,
            httpContextAccessorMock.Object,
            Mock.Of<ILogger<PipelineRunRecorder>>());

        mandate = new Mandate { Name = TestHelpers.Localized(nameof(PipelineRunRecorderTest)), FileTypes = [".xtf"], PipelineId = "pipe_a" };
        user = new User { FullName = nameof(PipelineRunRecorderTest), AuthIdentifier = Guid.NewGuid().ToString() };
        context.Mandates.Add(mandate);
        context.Users.Add(user);
        context.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup() => context.Dispose();

    [TestMethod]
    public async Task RecordJobStartedWritesRunWithManifest()
    {
        currentHttpContext = new DefaultHttpContext();
        currentHttpContext.Request.Headers.Cookie = "geopilot.auth=token";

        var job = NewJob(out var upload);

        await recorder.RecordJobStartedAsync(job, mandate, user, upload);

        var run = await context.PipelineRuns.Include(r => r.Files).SingleAsync(r => r.JobId == job.Id);
        Assert.AreEqual("pipe_a", run.PipelineId);
        Assert.AreEqual("{}", run.Definition);
        Assert.AreEqual(mandate.Id, run.MandateId);
        Assert.AreEqual(user.Id, run.UserId);
        Assert.AreEqual(ClientKind.WebClient, run.ClientKind);
        Assert.AreEqual(upload.Id, run.UploadId);
        Assert.AreEqual("https://storage.example.com/uploads", run.UploadStorageLocation);
        Assert.AreEqual(ScanState.NotScanned, run.ScanState);
        Assert.IsNull(run.TerminalState, "a fresh run has no terminal state yet.");
        Assert.AreNotEqual(string.Empty, run.AppVersion);

        Assert.HasCount(2, run.Files);
        var file = run.Files.Single(f => f.FileName == "data.xtf");
        Assert.AreEqual("uploads/1/data.xtf", file.StorageKey);
        Assert.AreEqual(1024, file.DeclaredSize);
        Assert.IsNull(file.Sha256, "hashes are computed by the scan, which has not run yet.");
    }

    [TestMethod]
    public async Task RecordJobStartedClassifiesTheClient()
    {
        var bearerContext = new DefaultHttpContext();
        bearerContext.Request.Headers.Authorization = "Bearer some-token";
        Assert.AreEqual(ClientKind.ApiClient, await ClassifyAsync(bearerContext));

        var browserContext = new DefaultHttpContext();
        browserContext.Request.Headers.Origin = "https://localhost:5173";
        Assert.AreEqual(ClientKind.WebClient, await ClassifyAsync(browserContext), "an anonymous browser request is identified by its fetch metadata.");

        var bareContext = new DefaultHttpContext();
        Assert.AreEqual(ClientKind.Unknown, await ClassifyAsync(bareContext));

        Assert.AreEqual(ClientKind.Unknown, await ClassifyAsync(null), "a write outside a request cannot classify.");
    }

    [TestMethod]
    public async Task RecordJobStartedThrowsWhenRecordCannotBeWritten()
    {
        // Deliberately hard: the caller must not start a job it cannot account for. A duplicate job id
        // violates the unique index, standing in for any failing write.
        currentHttpContext = null;
        var job = NewJob(out var upload);
        await recorder.RecordJobStartedAsync(job, mandate, user, upload);

        await Assert.ThrowsAsync<DbUpdateException>(() => recorder.RecordJobStartedAsync(job, mandate, user, upload));
    }

    [TestMethod]
    public async Task StepLifecycleUpsertsOneRowPerStep()
    {
        currentHttpContext = null;
        var job = NewJob(out var upload);
        await recorder.RecordJobStartedAsync(job, mandate, user, upload);

        var step = StepMock("validation");
        await recorder.RecordStepStartedAsync(job.Id, step.Object, 0);

        var row = await LoadStepRowAsync(job.Id, "validation");
        Assert.AreEqual(StepState.Running, row.State);
        Assert.IsNotNull(row.StartedAt);
        Assert.AreEqual(0, row.Order);
        Assert.AreEqual(typeof(object).FullName, row.ProcessImplementation);

        var startedAt = DateTime.UtcNow.AddSeconds(-2);
        var finishedAt = DateTime.UtcNow;
        step.SetupProperty(s => s.State, StepState.Warning);
        step.SetupGet(s => s.StartedAt).Returns(startedAt);
        step.SetupGet(s => s.FinishedAt).Returns(finishedAt);
        step.SetupGet(s => s.StatusMessage).Returns(TestHelpers.Localized("checked"));
        step.SetupGet(s => s.ConditionMessage).Returns(TestHelpers.Localized("warned"));
        step.SetupGet(s => s.ConditionEvaluations).Returns(new List<ConditionEvaluation>
        {
            new("skip-check", ConditionPhase.Pre, ConditionKind.Skip, "[a.B] == 999", false, new Dictionary<string, string?> { ["a.B"] = "123" }),
            new(null, ConditionPhase.Post, ConditionKind.Warn, "[a.B] == 123", true, new Dictionary<string, string?>()),
        });
        step.SetupGet(s => s.Downloads).Returns(new List<PersistedFile> { new("error.log", "validation_error.log") });
        step.SetupGet(s => s.Visualizations).Returns(new List<StepVisualization> { new("Visualization.json", "validation_Visualization.json") });

        await recorder.RecordStepCompletedAsync(job.Id, step.Object, 0);

        row = await LoadStepRowAsync(job.Id, "validation");
        Assert.AreEqual(StepState.Warning, row.State);
        Assert.AreEqual(startedAt, row.StartedAt);
        Assert.AreEqual(finishedAt, row.FinishedAt);
        Assert.IsNotNull(row.StatusMessage);
        Assert.HasCount(2, row.Conditions, "non-matching evaluations must be recorded too.");
        var skipped = row.Conditions.Single(c => c.ConditionId == "skip-check");
        Assert.IsFalse(skipped.Matched);
        Assert.AreEqual(ConditionPhase.Pre, skipped.Phase);
        Assert.AreEqual(ConditionKind.Skip, skipped.Kind);
        Assert.Contains("123", skipped.EvaluatedValues);
        Assert.HasCount(2, row.Artifacts);
        Assert.AreEqual(1, row.Artifacts.Count(a => a.Kind == ArtifactKind.Download));
        Assert.AreEqual(1, row.Artifacts.Count(a => a.Kind == ArtifactKind.Visualization));
    }

    [TestMethod]
    public async Task RecordRunFinishedReconcilesStepsAndSetsTerminalState()
    {
        currentHttpContext = null;
        var job = NewJob(out var upload);
        await recorder.RecordJobStartedAsync(job, mandate, user, upload);

        // The first step completed with a delivery file that was extracted only after the last step;
        // the second step was never reached, so no row exists for it yet.
        var completed = StepMock("validation");
        completed.SetupProperty(s => s.State, StepState.Success);
        completed.SetupGet(s => s.DeliveryFiles).Returns(new List<PersistedFile> { new("data.xtf", "validation_data.xtf", true) });
        await recorder.RecordStepStartedAsync(job.Id, completed.Object, 0);

        var neverReached = StepMock("packaging");

        await recorder.RecordRunFinishedAsync(
            job.Id,
            new List<IPipelineStep> { completed.Object, neverReached.Object },
            ProcessingState.Success,
            null);

        var run = await context.PipelineRunsWithIncludes.SingleAsync(r => r.JobId == job.Id);
        Assert.AreEqual(ProcessingState.Success, run.TerminalState);
        Assert.IsNotNull(run.TerminalAt);
        Assert.HasCount(2, run.Steps, "a step that never got a row must be reconciled at run end.");

        var deliveryArtifact = run.Steps.Single(s => s.StepId == "validation").Artifacts.Single();
        Assert.AreEqual(ArtifactKind.Delivery, deliveryArtifact.Kind);
        Assert.IsTrue(deliveryArtifact.FromUpload ?? false);

        var pendingRow = run.Steps.Single(s => s.StepId == "packaging");
        Assert.AreEqual(StepState.Pending, pendingRow.State);
        Assert.AreEqual(1, pendingRow.Order);
    }

    [TestMethod]
    public async Task RecordScanOutcomeSetsStateAndHashes()
    {
        currentHttpContext = null;
        var job = NewJob(out var upload);
        await recorder.RecordJobStartedAsync(job, mandate, user, upload);

        var scanResult = new ScanResult(
            IsClean: false,
            ThreatDetails: "eicar.xtf: Win.Test.EICAR_HDB-1",
            Hashes: new Dictionary<string, string> { ["uploads/1/data.xtf"] = "cafe01" });

        await recorder.RecordScanOutcomeAsync(job.Id, scanResult);

        var run = await context.PipelineRuns.Include(r => r.Files).SingleAsync(r => r.JobId == job.Id);
        Assert.AreEqual(ScanState.ThreatDetected, run.ScanState);
        Assert.Contains("EICAR", run.ScanDetails);
        Assert.AreEqual("cafe01", run.Files.Single(f => f.FileName == "data.xtf").Sha256);
        Assert.IsNull(run.Files.Single(f => f.FileName == "model.ili").Sha256, "a file without hash entry stays unset.");
    }

    [TestMethod]
    public async Task RecordScanOutcomeKeepsNotScannedWhenScanningIsDisabled()
    {
        currentHttpContext = null;
        var job = NewJob(out var upload);
        await recorder.RecordJobStartedAsync(job, mandate, user, upload);

        await recorder.RecordScanOutcomeAsync(job.Id, new ScanResult(true, Scanned: false));

        var run = await context.PipelineRuns.SingleAsync(r => r.JobId == job.Id);
        Assert.AreEqual(ScanState.NotScanned, run.ScanState, "a skipped scan must never be recorded as clean.");
    }

    [TestMethod]
    public async Task RecordPreflightFailedSetsTerminalFailed()
    {
        currentHttpContext = null;
        var job = NewJob(out var upload);
        await recorder.RecordJobStartedAsync(job, mandate, user, upload);

        await recorder.RecordPreflightFailedAsync(job.Id, "ThreatDetected: The uploaded files could not be processed.");

        var run = await context.PipelineRuns.SingleAsync(r => r.JobId == job.Id);
        Assert.AreEqual(ProcessingState.Failed, run.TerminalState);
        Assert.IsNotNull(run.TerminalAt);
        Assert.Contains("ThreatDetected", run.FailureReason);
    }

    [TestMethod]
    public async Task SoftWritesWithoutRunRecordDoNotThrow()
    {
        var unknownJobId = Guid.NewGuid();
        var step = StepMock("validation");

        await recorder.RecordStepStartedAsync(unknownJobId, step.Object, 0);
        await recorder.RecordStepCompletedAsync(unknownJobId, step.Object, 0);
        await recorder.RecordRunFinishedAsync(unknownJobId, new List<IPipelineStep> { step.Object }, ProcessingState.Failed, "boom");
        await recorder.RecordPreflightFailedAsync(unknownJobId, "boom");

        Assert.IsFalse(await context.PipelineRuns.AnyAsync(r => r.JobId == unknownJobId), "soft writes without a run record must be skipped, not fail.");
    }

    private async Task<ClientKind> ClassifyAsync(HttpContext? httpContext)
    {
        currentHttpContext = httpContext;
        var job = NewJob(out var upload);
        await recorder.RecordJobStartedAsync(job, mandate, user, upload);
        var run = await context.PipelineRuns.SingleAsync(r => r.JobId == job.Id);
        return run.ClientKind;
    }

    private static ProcessingJob NewJob(out UploadInfo upload)
    {
        var uploadId = Guid.NewGuid();
        upload = new UploadInfo(
            uploadId,
            ImmutableList.Create(
                new UploadedFileInfo("data.xtf", "uploads/1/data.xtf", 1024),
                new UploadedFileInfo("model.ili", "uploads/1/model.ili", 256)),
            DateTime.UtcNow);

        var pipeline = new Mock<IPipeline>();
        pipeline.SetupGet(p => p.Id).Returns("pipe_a");

        return new ProcessingJob(Guid.NewGuid(), uploadId, null, DateTime.UtcNow) { Pipeline = pipeline.Object };
    }

    private static Mock<IPipelineStep> StepMock(string id)
    {
        var step = new Mock<IPipelineStep>();
        step.SetupGet(s => s.Id).Returns(id);
        step.SetupGet(s => s.DisplayName).Returns(TestHelpers.Localized(id));
        step.SetupGet(s => s.Process).Returns(new object());
        step.SetupProperty(s => s.State, StepState.Pending);
        step.SetupGet(s => s.ConditionEvaluations).Returns(new List<ConditionEvaluation>());
        step.SetupGet(s => s.Downloads).Returns(new List<PersistedFile>());
        step.SetupGet(s => s.Visualizations).Returns(new List<StepVisualization>());
        step.SetupGet(s => s.DeliveryFiles).Returns(new List<PersistedFile>());
        return step;
    }

    private async Task<PipelineRunStep> LoadStepRowAsync(Guid jobId, string stepId)
    {
        var run = await context.PipelineRuns.SingleAsync(r => r.JobId == jobId);
        return await context.PipelineRunSteps
            .Include(s => s.Conditions)
            .Include(s => s.Artifacts)
            .SingleAsync(s => s.PipelineRunId == run.Id && s.StepId == stepId);
    }
}
