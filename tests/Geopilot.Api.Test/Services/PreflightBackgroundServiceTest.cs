using Geopilot.Api.Enums;
using Geopilot.Api.Exceptions;
using Geopilot.Api.FileAccess;
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
    private Mock<IUploadStore> uploadStoreMock;
    private Mock<ICloudOrchestrationService> cloudOrchestrationServiceMock;
    private Mock<ICloudStorageService> cloudStorageServiceMock;
    private Mock<IUploadFileStore> uploadFileStoreMock;
    private Mock<ILogger<PreflightBackgroundService>> loggerMock;
    private PreflightBackgroundService service;

    [TestInitialize]
    public void Initialize()
    {
        jobStoreMock = new Mock<IProcessingJobStore>(MockBehavior.Strict);
        uploadStoreMock = new Mock<IUploadStore>(MockBehavior.Strict);
        cloudOrchestrationServiceMock = new Mock<ICloudOrchestrationService>(MockBehavior.Strict);
        cloudStorageServiceMock = new Mock<ICloudStorageService>(MockBehavior.Strict);
        uploadFileStoreMock = new Mock<IUploadFileStore>(MockBehavior.Strict);
        loggerMock = new Mock<ILogger<PreflightBackgroundService>>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IProcessingJobStore))).Returns(jobStoreMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUploadStore))).Returns(uploadStoreMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICloudOrchestrationService))).Returns(cloudOrchestrationServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICloudStorageService))).Returns(cloudStorageServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUploadFileStore))).Returns(uploadFileStoreMock.Object);

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
        var mandateId = 1;

        var pipeline = new Mock<IPipeline>(MockBehavior.Strict);

        var pendingJob = new ProcessingJob(jobId, new List<ProcessingJobFile>(), mandateId, DateTime.Now)
        {
            Pipeline = pipeline.Object,
        };
        var stagedJob = new ProcessingJob(jobId, new List<ProcessingJobFile>() { new ProcessingJobFile("test.xtf", "random.xtf") }, mandateId, DateTime.Now)
        {
            Pipeline = pipeline.Object,
        };

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId)).Returns(Task.CompletedTask);
        cloudOrchestrationServiceMock.Setup(x => x.StageFilesLocallyAsync(uploadId, jobId)).ReturnsAsync(stagedJob);
        uploadFileStoreMock.Setup(x => x.Exists(jobId, "random.xtf")).Returns(true);
        uploadFileStoreMock.Setup(x => x.GetPath(jobId, "random.xtf")).Returns("path/to/random.xtf");
        jobStoreMock
            .Setup(x => x.EnqueueForProcessing(jobId, It.Is<IReadOnlyList<IPipelineFile>>(files => files.Any(f => f.OriginalFileName == "test.xtf"))))
            .Returns(stagedJob with { State = ProcessingState.Running });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        cloudOrchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(uploadId), Times.Once);
        cloudOrchestrationServiceMock.Verify(x => x.StageFilesLocallyAsync(uploadId, jobId), Times.Once);
        jobStoreMock.Verify(x => x.EnqueueForProcessing(jobId, It.Is<IReadOnlyList<IPipelineFile>>(files => files.Any(f => f.OriginalFileName == "test.xtf"))), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSetsFailedOnPreflightFailure()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pipeline = new Mock<IPipeline>();
        var pendingJob = new ProcessingJob(jobId, new List<ProcessingJobFile>(), 1, DateTime.Now)
        {
            Pipeline = pipeline.Object,
        };

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId))
            .ThrowsAsync(new CloudUploadPreflightException(PreflightFailureReason.IncompleteUpload, "File missing."));
        cloudStorageServiceMock.Setup(x => x.DeletePrefixAsync($"uploads/{uploadId}/")).Returns(Task.CompletedTask);
        uploadStoreMock.Setup(x => x.RemoveUpload(uploadId)).Returns(true);
        jobStoreMock.Setup(x => x.MarkAsFailed(jobId)).Returns(pendingJob with { State = ProcessingState.Failed });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        jobStoreMock.Verify(x => x.MarkAsFailed(jobId), Times.Once);
        cloudStorageServiceMock.Verify(x => x.DeletePrefixAsync($"uploads/{uploadId}/"), Times.Once);
        uploadStoreMock.Verify(x => x.RemoveUpload(uploadId), Times.Once);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSetsFailedOnGenericException()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pipeline = new Mock<IPipeline>();
        var pendingJob = new ProcessingJob(jobId, new List<ProcessingJobFile>(), 1, DateTime.Now)
        {
            Pipeline = pipeline.Object,
        };

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId))
            .ThrowsAsync(new InvalidOperationException("Network timeout"));
        cloudStorageServiceMock.Setup(x => x.DeletePrefixAsync($"uploads/{uploadId}/")).Returns(Task.CompletedTask);
        uploadStoreMock.Setup(x => x.RemoveUpload(uploadId)).Returns(true);
        jobStoreMock.Setup(x => x.MarkAsFailed(jobId)).Returns(pendingJob with { State = ProcessingState.Failed });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        jobStoreMock.Verify(x => x.MarkAsFailed(jobId), Times.Once);
        cloudStorageServiceMock.Verify(x => x.DeletePrefixAsync($"uploads/{uploadId}/"), Times.Once);
        uploadStoreMock.Verify(x => x.RemoveUpload(uploadId), Times.Once);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSetsFailedEvenWhenCleanupFails()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pipeline = new Mock<IPipeline>();
        var pendingJob = new ProcessingJob(jobId, new List<ProcessingJobFile>(), 1, DateTime.Now)
        {
            Pipeline = pipeline.Object,
        };

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId))
            .ThrowsAsync(new CloudUploadPreflightException(PreflightFailureReason.IncompleteUpload, "File missing."));
        cloudStorageServiceMock.Setup(x => x.DeletePrefixAsync($"uploads/{uploadId}/"))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable."));
        jobStoreMock.Setup(x => x.MarkAsFailed(jobId)).Returns(pendingJob with { State = ProcessingState.Failed });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        jobStoreMock.Verify(x => x.MarkAsFailed(jobId), Times.Once);
        cloudStorageServiceMock.Verify(x => x.DeletePrefixAsync($"uploads/{uploadId}/"), Times.Once);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSetsFailedWhenNoFilesStaged()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var pipeline = new Mock<IPipeline>();

        var pendingJob = new ProcessingJob(jobId, new List<ProcessingJobFile>(), 1, DateTime.Now)
        {
            Pipeline = pipeline.Object,
        };
        var stagedJob = new ProcessingJob(jobId, new List<ProcessingJobFile>(), 1, DateTime.Now)
        {
            Pipeline = pipeline.Object,
        };

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(pendingJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId)).Returns(Task.CompletedTask);
        cloudOrchestrationServiceMock.Setup(x => x.StageFilesLocallyAsync(uploadId, jobId)).ReturnsAsync(stagedJob);
        cloudStorageServiceMock.Setup(x => x.DeletePrefixAsync($"uploads/{uploadId}/")).Returns(Task.CompletedTask);
        uploadStoreMock.Setup(x => x.RemoveUpload(uploadId)).Returns(true);
        jobStoreMock.Setup(x => x.MarkAsFailed(jobId)).Returns(pendingJob with { State = ProcessingState.Failed });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        cloudStorageServiceMock.Verify(x => x.DeletePrefixAsync($"uploads/{uploadId}/"), Times.Once);
        uploadStoreMock.Verify(x => x.RemoveUpload(uploadId), Times.Once);
        jobStoreMock.Verify(x => x.MarkAsFailed(jobId), Times.Once);
        jobStoreMock.Verify(x => x.EnqueueForProcessing(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<IPipelineFile>>()), Times.Never);
        pipeline.Verify(p => p.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSkipsJobNoLongerPending()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var alreadyProcessingJob = new ProcessingJob(jobId, new List<ProcessingJobFile>() { new ProcessingJobFile("test.xtf", "random.xtf") }, 1, DateTime.Now)
        {
            Pipeline = new Mock<IPipeline>().Object,
            State = ProcessingState.Running,
        };

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(alreadyProcessingJob);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        cloudOrchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSkipsMissingJob()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns((ProcessingJob?)null);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, uploadId));

        cloudOrchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(It.IsAny<Guid>()), Times.Never);
    }
}
