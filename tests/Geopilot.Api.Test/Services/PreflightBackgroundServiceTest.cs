using Geopilot.Api.Enums;
using Geopilot.Api.Exceptions;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Threading.Channels;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class PreflightBackgroundServiceTest
{
    private Mock<IProcessingJobStore> jobStoreMock;
    private Mock<IUploadOrchestrationService> orchestrationServiceMock;
    private Mock<IPipelineRunRecorder> runRecorderMock;
    private Mock<ILogger<PreflightBackgroundService>> loggerMock;
    private PreflightBackgroundService service;

    [TestInitialize]
    public void Initialize()
    {
        jobStoreMock = new Mock<IProcessingJobStore>(MockBehavior.Strict);
        orchestrationServiceMock = new Mock<IUploadOrchestrationService>(MockBehavior.Strict);
        runRecorderMock = new Mock<IPipelineRunRecorder>();
        loggerMock = new Mock<ILogger<PreflightBackgroundService>>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IProcessingJobStore))).Returns(jobStoreMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUploadOrchestrationService))).Returns(orchestrationServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IPipelineRunRecorder))).Returns(runRecorderMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        // The real lifecycle, not a mock: it resolves the recorder and the orchestration from the same scope
        // factory, so these tests still see every consequence of a preflight.
        var lifecycle = new JobLifecycle(Mock.Of<ILogger<JobLifecycle>>(), jobStoreMock.Object, scopeFactoryMock.Object, Options.Create(new ProcessingOptions { JobTimeout = TimeSpan.FromMinutes(5) }));

        var channel = Channel.CreateUnbounded<PreflightRequest>();
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
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId)).ReturnsAsync(scanResult);
        orchestrationServiceMock.Setup(x => x.RegisterJobFiles(uploadId, jobId)).Returns(registeredFiles);
        jobStoreMock
            .Setup(x => x.EnqueueForProcessing(jobId, registeredFiles))
            .Returns(pendingJob with { State = ProcessingState.Running });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        // The preflight runs the checks and hands the registered files on; what follows is JobLifecycleTest's.
        orchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(uploadId), Times.Once);
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
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId))
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
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId))
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
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId))
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
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId)).ReturnsAsync(scanResult);
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

        orchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSkipsMissingJob()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns((ProcessingJob?)null);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        orchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(It.IsAny<Guid>()), Times.Never);
    }

    private static ProcessingJob CreatePendingJob(Guid jobId, Guid uploadId, IPipeline pipeline)
        => new ProcessingJob(jobId, uploadId, 1, DateTime.Now) { Pipeline = pipeline };

    private static IPipelineFile PipelineFileNamed(string originalFileName)
    {
        var fileMock = new Mock<IPipelineFile>();
        fileMock.SetupGet(f => f.OriginalFileName).Returns(originalFileName);
        return fileMock.Object;
    }
}
