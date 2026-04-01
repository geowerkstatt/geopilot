using Geopilot.Api.Enums;
using Geopilot.Api.Exceptions;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Services;
using Geopilot.Api.Validation;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Immutable;
using System.Threading.Channels;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class PreflightBackgroundServiceTest
{
    private Mock<IValidationJobStore> jobStoreMock;
    private Mock<ICloudOrchestrationService> cloudOrchestrationServiceMock;
    private Mock<ICloudStorageService> cloudStorageServiceMock;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IFileProvider> fileProviderMock;
    private Mock<IPipelineFactory> pipelineFactoryMock;
    private Mock<ILogger<PreflightBackgroundService>> loggerMock;
    private Context context;
    private PreflightBackgroundService service;

    [TestInitialize]
    public void Initialize()
    {
        jobStoreMock = new Mock<IValidationJobStore>(MockBehavior.Strict);
        cloudOrchestrationServiceMock = new Mock<ICloudOrchestrationService>(MockBehavior.Strict);
        cloudStorageServiceMock = new Mock<ICloudStorageService>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);
        pipelineFactoryMock = new Mock<IPipelineFactory>(MockBehavior.Strict);
        loggerMock = new Mock<ILogger<PreflightBackgroundService>>();
        context = AssemblyInitialize.DbFixture.GetTestContext();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IValidationJobStore))).Returns(jobStoreMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICloudOrchestrationService))).Returns(cloudOrchestrationServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICloudStorageService))).Returns(cloudStorageServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IMandateService))).Returns(mandateServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IFileProvider))).Returns(fileProviderMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IPipelineFactory))).Returns(pipelineFactoryMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(Context))).Returns(context);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var channel = Channel.CreateUnbounded<PreflightRequest>();
        service = new PreflightBackgroundService(channel.Reader, scopeFactoryMock.Object, loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        context.Dispose();
    }

    [TestMethod]
    public async Task ProcessRequestAsyncHappyPath()
    {
        var jobId = Guid.NewGuid();
        var mandateId = 1;
        var pipelineId = "pipeline1";
        var userAuthId = "auth-123";

        var user = new User { AuthIdentifier = userAuthId, FullName = "Test User" };
        context.Users.Add(user);
        context.SaveChanges();

        var mandate = new Mandate { Id = mandateId, Name = "Test Mandate", PipelineId = pipelineId };

        var cloudJob = new ValidationJob(jobId, new List<ValidationJobFile>(), null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.VerifyingUpload, DateTime.Now, UploadMethod.Cloud);
        var stagedJob = new ValidationJob(jobId, new List<ValidationJobFile>() { new ValidationJobFile("test.xtf", "random.xtf") }, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now, UploadMethod.Cloud);
        var startedJob = new ValidationJob(jobId, new List<ValidationJobFile>() { new ValidationJobFile("test.xtf", "random.xtf") }, mandateId, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Processing, DateTime.Now, UploadMethod.Cloud);

        var pipeline = new Mock<IPipeline>(MockBehavior.Strict);

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(cloudJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(jobId)).Returns(Task.CompletedTask);
        cloudOrchestrationServiceMock.Setup(x => x.StageFilesLocallyAsync(jobId)).ReturnsAsync(stagedJob);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, It.Is<User>(u => u.AuthIdentifier == userAuthId))).ReturnsAsync(mandate);
        fileProviderMock.Setup(x => x.Initialize(jobId));
        fileProviderMock.Setup(x => x.GetFilePath("random.xtf")).Returns("path/to/random.xtf");
        pipelineFactoryMock.Setup(x => x.CreatePipeline(pipelineId, It.Is<List<IPipelineFile>>(files => files.Any(f => f.OriginalFileName == "test.xtf")), jobId)).Returns(pipeline.Object);
        jobStoreMock.Setup(x => x.StartJob(jobId, pipeline.Object, mandateId)).Returns(startedJob);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, mandateId, userAuthId));

        cloudOrchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(jobId), Times.Once);
        cloudOrchestrationServiceMock.Verify(x => x.StageFilesLocallyAsync(jobId), Times.Once);
        jobStoreMock.Verify(x => x.StartJob(jobId, pipeline.Object, mandateId), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncHandlesNullUserAuthId()
    {
        var jobId = Guid.NewGuid();
        var mandateId = 1;
        var pipelineId = "pipeline1";

        var mandate = new Mandate { Id = mandateId, Name = "Test Mandate", PipelineId = pipelineId };

        var cloudJob = new ValidationJob(jobId, new List<ValidationJobFile>(), null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.VerifyingUpload, DateTime.Now, UploadMethod.Cloud);
        var stagedJob = new ValidationJob(jobId, new List<ValidationJobFile>() { new ValidationJobFile("test.xtf", "random.xtf") }, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now, UploadMethod.Cloud);
        var startedJob = new ValidationJob(jobId, new List<ValidationJobFile>() { new ValidationJobFile("test.xtf", "random.xtf") }, mandateId, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Processing, DateTime.Now, UploadMethod.Cloud);

        var pipeline = new Mock<IPipeline>(MockBehavior.Strict);

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(cloudJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(jobId)).Returns(Task.CompletedTask);
        cloudOrchestrationServiceMock.Setup(x => x.StageFilesLocallyAsync(jobId)).ReturnsAsync(stagedJob);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, null)).ReturnsAsync(mandate);
        fileProviderMock.Setup(x => x.Initialize(jobId));
        fileProviderMock.Setup(x => x.GetFilePath("random.xtf")).Returns("path/to/random.xtf");
        pipelineFactoryMock.Setup(x => x.CreatePipeline(pipelineId, It.Is<ICollection<IPipelineFile>>(f => f.Any(file => file.OriginalFileName == "test.xtf")), jobId)).Returns(pipeline.Object);
        jobStoreMock.Setup(x => x.StartJob(jobId, pipeline.Object, mandateId)).Returns(startedJob);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, mandateId, null));

        cloudOrchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(jobId), Times.Once);
        cloudOrchestrationServiceMock.Verify(x => x.StageFilesLocallyAsync(jobId), Times.Once);
        jobStoreMock.Verify(x => x.StartJob(jobId, pipeline.Object, mandateId), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSetsFailedOnPreflightFailure()
    {
        var jobId = Guid.NewGuid();
        var cloudJob = new ValidationJob(jobId, new List<ValidationJobFile>(), null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.VerifyingUpload, DateTime.Now, UploadMethod.Cloud);

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(cloudJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(jobId))
            .ThrowsAsync(new CloudUploadPreflightException(PreflightFailureReason.IncompleteUpload, "File missing."));
        cloudStorageServiceMock.Setup(x => x.DeletePrefixAsync($"uploads/{jobId}/")).Returns(Task.CompletedTask);
        jobStoreMock.Setup(x => x.Failed(jobId)).Returns(cloudJob with { Status = Status.Failed });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, 1, null));

        jobStoreMock.Verify(x => x.Failed(jobId), Times.Once);
        cloudStorageServiceMock.Verify(x => x.DeletePrefixAsync($"uploads/{jobId}/"), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSetsFailedOnGenericException()
    {
        var jobId = Guid.NewGuid();
        var cloudJob = new ValidationJob(jobId, new List<ValidationJobFile>(), null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.VerifyingUpload, DateTime.Now, UploadMethod.Cloud);

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(cloudJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(jobId))
            .ThrowsAsync(new InvalidOperationException("Network timeout"));
        cloudStorageServiceMock.Setup(x => x.DeletePrefixAsync($"uploads/{jobId}/")).Returns(Task.CompletedTask);
        jobStoreMock.Setup(x => x.Failed(jobId)).Returns(cloudJob with { Status = Status.Failed });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, 1, null));

        jobStoreMock.Verify(x => x.Failed(jobId), Times.Once);
        cloudStorageServiceMock.Verify(x => x.DeletePrefixAsync($"uploads/{jobId}/"), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSetsFailedEvenWhenCleanupFails()
    {
        var jobId = Guid.NewGuid();
        var cloudJob = new ValidationJob(jobId, new List<ValidationJobFile>(), null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.VerifyingUpload, DateTime.Now, UploadMethod.Cloud);

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(cloudJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(jobId))
            .ThrowsAsync(new CloudUploadPreflightException(PreflightFailureReason.IncompleteUpload, "File missing."));
        cloudStorageServiceMock.Setup(x => x.DeletePrefixAsync($"uploads/{jobId}/"))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable."));
        jobStoreMock.Setup(x => x.Failed(jobId)).Returns(cloudJob with { Status = Status.Failed });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, 1, null));

        jobStoreMock.Verify(x => x.Failed(jobId), Times.Once);
        cloudStorageServiceMock.Verify(x => x.DeletePrefixAsync($"uploads/{jobId}/"), Times.Once);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSetsFailedWhenMandateResolutionFailsAfterStaging()
    {
        var jobId = Guid.NewGuid();
        var mandateId = 1;
        var userAuthId = "auth-123";

        var user = new User { AuthIdentifier = userAuthId, FullName = "Test User" };
        context.Users.Add(user);
        context.SaveChanges();

        var cloudJob = new ValidationJob(jobId, new List<ValidationJobFile>(), null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.VerifyingUpload, DateTime.Now, UploadMethod.Cloud);
        var stagedJob = new ValidationJob(jobId, new List<ValidationJobFile>() { new ValidationJobFile("test.xtf", "random.xtf") }, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now, UploadMethod.Cloud);

        var mandate = new Mandate { Id = mandateId, Name = "Test Mandate", PipelineId = null };

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(cloudJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(jobId)).Returns(Task.CompletedTask);
        cloudOrchestrationServiceMock.Setup(x => x.StageFilesLocallyAsync(jobId)).ReturnsAsync(stagedJob);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, It.Is<User>(u => u.AuthIdentifier == userAuthId))).ReturnsAsync(mandate);
        cloudStorageServiceMock.Setup(x => x.DeletePrefixAsync($"uploads/{jobId}/")).Returns(Task.CompletedTask);
        jobStoreMock.Setup(x => x.Failed(jobId)).Returns(cloudJob with { Status = Status.Failed });

        await service.ProcessRequestAsync(new PreflightRequest(jobId, mandateId, userAuthId));

        cloudStorageServiceMock.Verify(x => x.DeletePrefixAsync($"uploads/{jobId}/"), Times.Once);
        jobStoreMock.Verify(x => x.Failed(jobId), Times.Once);
        jobStoreMock.Verify(x => x.StartJob(It.IsAny<Guid>(), It.IsAny<IPipeline>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSkipsDuplicateMessage()
    {
        var jobId = Guid.NewGuid();
        var alreadyProcessingJob = new ValidationJob(jobId, new List<ValidationJobFile>() { new ValidationJobFile("test.xtf", "random.xtf") }, 1, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Processing, DateTime.Now, UploadMethod.Cloud);

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns(alreadyProcessingJob);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, 1, null));

        cloudOrchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessRequestAsyncSkipsMissingJob()
    {
        var jobId = Guid.NewGuid();

        jobStoreMock.Setup(x => x.GetJob(jobId)).Returns((ValidationJob?)null);

        await service.ProcessRequestAsync(new PreflightRequest(jobId, 1, null));

        cloudOrchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(It.IsAny<Guid>()), Times.Never);
    }
}
