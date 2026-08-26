using Geopilot.Api.Enums;
using Geopilot.Api.Exceptions;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        var channel = Channel.CreateUnbounded<PreflightRequest>();
        service = new PreflightBackgroundService(channel.Reader, scopeFactoryMock.Object, loggerMock.Object);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncHappyPath()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pendingJob = CreatePendingJob(jobId, uploadId, new Mock<IPipeline>(MockBehavior.Strict).Object);

        var registeredFiles = new List<IPipelineFile> { PipelineFileNamed("test.xtf") };

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId)).Returns(Task.CompletedTask);
        orchestrationServiceMock.Setup(x => x.RegisterJobFiles(uploadId, jobId)).Returns(registeredFiles);
        jobStoreMock
            .Setup(x => x.EnqueueForProcessing(jobId, registeredFiles))
            .Returns(pendingJob with { State = ProcessingState.Running });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        orchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(uploadId), Times.Once);
        orchestrationServiceMock.Verify(x => x.RegisterJobFiles(uploadId, jobId), Times.Once);
        jobStoreMock.Verify(x => x.EnqueueForProcessing(jobId, registeredFiles), Times.Once);

        // Nothing is transferred and the blobs stay: the files are fetched when a step reads them.
        orchestrationServiceMock.Verify(x => x.ReleaseUploadAsync(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSetsFailedOnPreflightFailure()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pipeline = new Mock<IPipeline>();
        var pendingJob = CreatePendingJob(jobId, uploadId, pipeline.Object);

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId))
            .ThrowsAsync(new UploadPreflightException(PreflightFailureReason.IncompleteUpload, "File missing."));
        orchestrationServiceMock.Setup(x => x.ReleaseUploadAsync(uploadId)).Returns(Task.CompletedTask);
        jobStoreMock.Setup(x => x.MarkAsFailed(jobId)).Returns(pendingJob with { State = ProcessingState.Failed });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        jobStoreMock.Verify(x => x.MarkAsFailed(jobId), Times.Once);
        orchestrationServiceMock.Verify(x => x.ReleaseUploadAsync(uploadId), Times.Once);
        pipeline.Verify(p => p.Dispose(), Times.Once);
        runRecorderMock.Verify(
            r => r.RecordPreflightFailedAsync(jobId, It.Is<string>(reason => reason.Contains(nameof(PreflightFailureReason.IncompleteUpload)) && reason.Contains("File missing."))),
            Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSetsFailedOnGenericException()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pipeline = new Mock<IPipeline>();
        var pendingJob = CreatePendingJob(jobId, uploadId, pipeline.Object);

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId))
            .ThrowsAsync(new InvalidOperationException("Network timeout"));
        orchestrationServiceMock.Setup(x => x.ReleaseUploadAsync(uploadId)).Returns(Task.CompletedTask);
        jobStoreMock.Setup(x => x.MarkAsFailed(jobId)).Returns(pendingJob with { State = ProcessingState.Failed });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        jobStoreMock.Verify(x => x.MarkAsFailed(jobId), Times.Once);
        orchestrationServiceMock.Verify(x => x.ReleaseUploadAsync(uploadId), Times.Once);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSetsFailedWhenRegisteringTheFilesFails()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pipeline = new Mock<IPipeline>();
        var pendingJob = CreatePendingJob(jobId, uploadId, pipeline.Object);

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId)).Returns(Task.CompletedTask);
        orchestrationServiceMock.Setup(x => x.RegisterJobFiles(uploadId, jobId))
            .Throws(new InvalidOperationException($"Upload <{uploadId}> has no files to register."));
        orchestrationServiceMock.Setup(x => x.ReleaseUploadAsync(uploadId)).Returns(Task.CompletedTask);
        jobStoreMock.Setup(x => x.MarkAsFailed(jobId)).Returns(pendingJob with { State = ProcessingState.Failed });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        jobStoreMock.Verify(x => x.MarkAsFailed(jobId), Times.Once);
        jobStoreMock.Verify(x => x.EnqueueForProcessing(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<IPipelineFile>>()), Times.Never);
        orchestrationServiceMock.Verify(x => x.ReleaseUploadAsync(uploadId), Times.Once);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSetsFailedEvenWhenCleanupFails()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pipeline = new Mock<IPipeline>();
        var pendingJob = CreatePendingJob(jobId, uploadId, pipeline.Object);

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId))
            .ThrowsAsync(new UploadPreflightException(PreflightFailureReason.IncompleteUpload, "File missing."));
        orchestrationServiceMock.Setup(x => x.ReleaseUploadAsync(uploadId))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable."));
        jobStoreMock.Setup(x => x.MarkAsFailed(jobId)).Returns(pendingJob with { State = ProcessingState.Failed });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        jobStoreMock.Verify(x => x.MarkAsFailed(jobId), Times.Once);
        pipeline.Verify(p => p.Dispose(), Times.Once);
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
