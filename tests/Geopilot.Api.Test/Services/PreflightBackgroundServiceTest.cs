using Geopilot.Api.Enums;
using Geopilot.Api.Exceptions;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Immutable;
using System.Threading.Channels;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class PreflightBackgroundServiceTest
{
    private Mock<IProcessingJobStore> jobStoreMock;
    private Mock<IUploadOrchestrationService> orchestrationServiceMock;
    private Mock<IRecorder> runRecorderMock;
    private Mock<ILogger<PreflightBackgroundService>> loggerMock;
    private Mock<IPipelineFactory> pipelineFactoryMock;
    private JobLifecycle lifecycle;
    private PreflightBackgroundService service;

    [TestInitialize]
    public void Initialize()
    {
        jobStoreMock = new Mock<IProcessingJobStore>(MockBehavior.Strict);
        orchestrationServiceMock = new Mock<IUploadOrchestrationService>(MockBehavior.Strict);
        runRecorderMock = new Mock<IRecorder>();
        loggerMock = new Mock<ILogger<PreflightBackgroundService>>();
        pipelineFactoryMock = new Mock<IPipelineFactory>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IProcessingJobStore))).Returns(jobStoreMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUploadOrchestrationService))).Returns(orchestrationServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IRecorder))).Returns(runRecorderMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        // The real lifecycle, not a mock: it resolves the recorder and the orchestration from the same scope
        // factory, so these tests still see every consequence of a preflight.
        var channel = Channel.CreateUnbounded<PreflightRequest>();
        lifecycle = new JobLifecycle(Mock.Of<ILogger<JobLifecycle>>(), jobStoreMock.Object, pipelineFactoryMock.Object, channel.Writer, scopeFactoryMock.Object, Options.Create(new ProcessingOptions { JobTimeout = TimeSpan.FromMinutes(5) }), Options.Create(new RefMappingOptions()));

        service = new PreflightBackgroundService(channel.Reader, scopeFactoryMock.Object, loggerMock.Object, lifecycle);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncHappyPath()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pendingJob = CreatePendingJob(jobId, uploadId, new Mock<IPipeline>(MockBehavior.Strict).Object);

        var registeredFiles = new List<IPipelineFile> { PipelineFileNamed("test.xtf") };
        var scanResult = new ScanResult(true, Hashes: new Dictionary<string, string> { ["uploads/key"] = "abc123" });

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId, It.IsAny<CancellationToken>())).ReturnsAsync(scanResult);
        orchestrationServiceMock.Setup(x => x.RegisterJobFiles(uploadId, jobId)).Returns(registeredFiles);
        jobStoreMock
            .Setup(x => x.EnqueueForProcessing(jobId, registeredFiles))
            .Returns(pendingJob with { State = ProcessingState.Running });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        // The preflight runs the checks and hands the registered files on; what follows is JobLifecycleTest's.
        orchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(uploadId, It.IsAny<CancellationToken>()), Times.Once);
        orchestrationServiceMock.Verify(x => x.RegisterJobFiles(uploadId, jobId), Times.Once);
        jobStoreMock.Verify(x => x.EnqueueForProcessing(jobId, registeredFiles), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncLeavesJobAndUploadUntouchedWhenTheHostShutsDown()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pendingJob = CreatePendingJob(jobId, uploadId, new Mock<IPipeline>(MockBehavior.Strict).Object);
        using var shutdown = new CancellationTokenSource();

        // The scan only ends when the shutdown cancels it, like a scan over several gigabytes would.
        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        orchestrationServiceMock
            .Setup(x => x.RunPreflightChecksAsync(uploadId, It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>(async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new ScanResult(true);
            });

        var processing = service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId), shutdown.Token);
        await shutdown.CancelAsync();
        await processing.WaitAsync(TimeSpan.FromSeconds(10));

        // The strict mocks refuse any other call, so the job is neither failed nor queued and no file is released.
        runRecorderMock.Verify(r => r.RecordPreflightFailedAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        runRecorderMock.Verify(r => r.RecordScanOutcomeAsync(It.IsAny<Guid>(), It.IsAny<ScanResult>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncRecordsScanOutcomeOfDetectedThreat()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pipeline = new Mock<IPipeline>();
        var pendingJob = CreatePendingJob(jobId, uploadId, pipeline.Object);
        var scanResult = new ScanResult(false, "eicar.xtf: Win.Test.EICAR_HDB-1");

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UploadPreflightException(PreflightFailureReason.ThreatDetected, "The uploaded files could not be processed.", scanResult));
        orchestrationServiceMock.Setup(x => x.ReleaseUploadAsync(uploadId)).Returns(Task.CompletedTask);
        jobStoreMock.Setup(x => x.TryMarkAsFailed(jobId)).Returns(true);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        runRecorderMock.Verify(r => r.RecordScanOutcomeAsync(jobId, scanResult), Times.Once, "the rejected upload is the forensically relevant case.");
        runRecorderMock.Verify(
            r => r.RecordPreflightFailedAsync(jobId, It.Is<string>(reason => reason.Contains(nameof(PreflightFailureReason.ThreatDetected)))),
            Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncReportsTheReasonOfAPreflightException()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pendingJob = CreatePendingJob(jobId, uploadId, new Mock<IPipeline>().Object);

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UploadPreflightException(PreflightFailureReason.IncompleteUpload, "File missing."));
        orchestrationServiceMock.Setup(x => x.ReleaseUploadAsync(uploadId)).Returns(Task.CompletedTask);
        jobStoreMock.Setup(x => x.TryMarkAsFailed(jobId)).Returns(true);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        runRecorderMock.Verify(r => r.RecordPreflightFailedAsync(jobId, "IncompleteUpload: File missing."), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncReportsTheMessageOfAnUnexpectedException()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pendingJob = CreatePendingJob(jobId, uploadId, new Mock<IPipeline>().Object);

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Network timeout"));
        orchestrationServiceMock.Setup(x => x.ReleaseUploadAsync(uploadId)).Returns(Task.CompletedTask);
        jobStoreMock.Setup(x => x.TryMarkAsFailed(jobId)).Returns(true);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        runRecorderMock.Verify(r => r.RecordPreflightFailedAsync(jobId, "Network timeout"), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncKeepsThePassedScanWhenRegisteringTheFilesFails()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pendingJob = CreatePendingJob(jobId, uploadId, new Mock<IPipeline>().Object);
        var scanResult = new ScanResult(true, Hashes: new Dictionary<string, string> { ["uploads/key"] = "abc123" });

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId, It.IsAny<CancellationToken>())).ReturnsAsync(scanResult);
        orchestrationServiceMock.Setup(x => x.RegisterJobFiles(uploadId, jobId))
            .Throws(new InvalidOperationException($"Upload <{uploadId}> has no files to register."));
        orchestrationServiceMock.Setup(x => x.ReleaseUploadAsync(uploadId)).Returns(Task.CompletedTask);
        jobStoreMock.Setup(x => x.TryMarkAsFailed(jobId)).Returns(true);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        // The scan passed before registering failed, so its outcome and hashes still belong in the protocol.
        runRecorderMock.Verify(r => r.RecordScanOutcomeAsync(jobId, scanResult), Times.Once);
        runRecorderMock.Verify(r => r.RecordPreflightFailedAsync(jobId, $"Upload <{uploadId}> has no files to register."), Times.Once);
        jobStoreMock.Verify(x => x.EnqueueForProcessing(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<IPipelineFile>>()), Times.Never);
    }

    [TestMethod]
    public async Task UnrecordablePreflightKeepsTheUpload()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pipeline = new Mock<IPipeline>();
        var pendingJob = CreatePendingJob(jobId, uploadId, pipeline.Object);
        var scanResult = new ScanResult(true);
        var unavailable = new RecordingUnavailableException("that the preflight passed", new TimeoutException());

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId, It.IsAny<CancellationToken>())).ReturnsAsync(scanResult);
        orchestrationServiceMock.Setup(x => x.RegisterJobFiles(uploadId, jobId)).Returns(new List<IPipelineFile>());
        orchestrationServiceMock.Setup(x => x.ReleaseUploadAsync(uploadId)).Returns(Task.CompletedTask);
        runRecorderMock.Setup(r => r.RecordPreflightPassedAsync(jobId, scanResult, It.IsAny<CancellationToken>())).ThrowsAsync(unavailable);
        jobStoreMock.Setup(x => x.TryMarkAsCancelled(jobId)).Returns(true);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        // The database interrupted the preflight, not the data: the job ends like after a timeout and keeps its upload.
        jobStoreMock.Verify(x => x.TryMarkAsCancelled(jobId), Times.Once);
        runRecorderMock.Verify(
            r => r.RecordRunFinishedAsync(jobId, It.IsAny<IReadOnlyList<IPipelineStep>>(), ProcessingState.Cancelled, unavailable.Message, It.IsAny<CancellationToken>()),
            Times.Once);
        pipeline.Verify(p => p.Dispose(), Times.Once);
        orchestrationServiceMock.Verify(x => x.ReleaseUploadAsync(It.IsAny<Guid>()), Times.Never);
        runRecorderMock.Verify(r => r.RecordPreflightFailedAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never, "The preflight did not fail.");
    }

    [TestMethod]
    public async Task AbortedQueuedJobEndsCancelledAndKeepsItsUpload()
    {
        var uploadId = Guid.NewGuid();
        var pipeline = new Mock<IPipeline>();
        var jobId = await StartHeldJobAsync(uploadId, pipeline.Object);
        jobStoreMock.Setup(x => x.TryMarkAsCancelled(jobId)).Returns(true);

        // The heartbeat aborts the job while it still waits in the preflight queue.
        lifecycle.Abort([jobId], "heartbeat lost");
        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        orchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never, "An aborted job is not checked any more.");
        AssertAbortedAndKeptItsUpload(jobId, pipeline, "heartbeat lost");
    }

    [TestMethod]
    public async Task AbortStopsTheRunningPreflightAndKeepsTheUpload()
    {
        var uploadId = Guid.NewGuid();
        var pipeline = new Mock<IPipeline>();
        var jobId = await StartHeldJobAsync(uploadId, pipeline.Object);
        jobStoreMock.Setup(x => x.TryMarkAsCancelled(jobId)).Returns(true);

        // The scan only ends when its token is cancelled; the abort comes while it runs.
        orchestrationServiceMock
            .Setup(x => x.RunPreflightChecksAsync(uploadId, It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>(async (_, cancellationToken) =>
            {
                lifecycle.Abort([jobId], "database unreachable");
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new ScanResult(true);
            });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId)).WaitAsync(TimeSpan.FromSeconds(10));

        AssertAbortedAndKeptItsUpload(jobId, pipeline, "database unreachable");
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSkipsJobNoLongerPending()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var alreadyProcessingJob = CreatePendingJob(jobId, uploadId, new Mock<IPipeline>().Object) with
        {
            State = ProcessingState.Running,
        };

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(alreadyProcessingJob);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        orchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSkipsMissingJob()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns((ProcessingJob?)null);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        orchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ProcessingJob CreatePendingJob(Guid jobId, Guid uploadId, IPipeline pipeline)
        => new ProcessingJob(jobId, uploadId, 1, DateTime.Now) { Pipeline = pipeline };

    /// <summary>
    /// Starts a pending job through the lifecycle, which holds it from then on as after a real start.
    /// </summary>
    private async Task<Guid> StartHeldJobAsync(Guid uploadId, IPipeline pipeline)
    {
        var jobId = Guid.NewGuid();
        var pendingJob = CreatePendingJob(jobId, uploadId, pipeline);
        jobStoreMock.Setup(x => x.CreateJob(uploadId, true, null)).Returns(pendingJob with { Pipeline = null });
        pipelineFactoryMock.Setup(f => f.CreatePipeline("pipe_a", jobId)).Returns(pipeline);
        jobStoreMock.Setup(x => x.AttachPipeline(jobId, pipeline, 1)).Returns(pendingJob);
        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);

        var upload = new UploadInfo(uploadId, ImmutableList<UploadedFileInfo>.Empty, DateTime.UtcNow);
        await lifecycle.StartAsync(upload, new Mandate { Id = 1 }, "pipe_a", supplier: null, mayDeliver: true, deliveryFields: null);
        return jobId;
    }

    /// <summary>
    /// An aborted preflight ends like an interrupted one: the job is cancelled with the reason of the abort and keeps its
    /// upload, because another instance may already process the same upload again.
    /// </summary>
    private void AssertAbortedAndKeptItsUpload(Guid jobId, Mock<IPipeline> pipeline, string reason)
    {
        jobStoreMock.Verify(x => x.TryMarkAsCancelled(jobId), Times.Once);
        runRecorderMock.Verify(
            r => r.RecordRunFinishedAsync(jobId, It.IsAny<IReadOnlyList<IPipelineStep>>(), ProcessingState.Cancelled, reason, It.IsAny<CancellationToken>()),
            Times.Once);
        pipeline.Verify(p => p.Dispose(), Times.Once);
        orchestrationServiceMock.Verify(x => x.ReleaseUploadAsync(It.IsAny<Guid>()), Times.Never);
        runRecorderMock.Verify(r => r.RecordPreflightFailedAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never, "The preflight did not fail.");
        Assert.IsEmpty(lifecycle.HeldJobIds, "The lifecycle lets go of a job once its end is carried out.");
    }

    private static IPipelineFile PipelineFileNamed(string originalFileName)
    {
        var fileMock = new Mock<IPipelineFile>();
        fileMock.SetupGet(f => f.OriginalFileName).Returns(originalFileName);
        return fileMock.Object;
    }
}
