using Geopilot.Api.Enums;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Services;
using Geopilot.Api.Validation;
using Geopilot.PipelineCore.Pipeline;
using Moq;
using System.Collections.Immutable;
using System.Threading.Channels;

namespace Geopilot.Api.Test.Validation;

[TestClass]
public class ValidationServiceTest
{
    private Mock<IFileProvider> fileProviderMock;
    private Mock<ICloudOrchestrationService> cloudOrchestrationServiceMock;
    private Context context;
    private ValidationService validationService;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IValidationJobStore> validationJobStoreMock;
    private Mock<IPipelineFactory> pipelineFactoryMock;
    private Channel<PreflightRequest> preflightQueue;

    [TestInitialize]
    public void Initialize()
    {
        fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);
        cloudOrchestrationServiceMock = new Mock<ICloudOrchestrationService>(MockBehavior.Strict);
        context = AssemblyInitialize.DbFixture.GetTestContext();
        validationJobStoreMock = new Mock<IValidationJobStore>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        pipelineFactoryMock = new Mock<IPipelineFactory>(MockBehavior.Strict);
        preflightQueue = Channel.CreateUnbounded<PreflightRequest>();

        validationService = new ValidationService(
            validationJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            pipelineFactoryMock.Object,
            cloudOrchestrationServiceMock.Object,
            preflightQueue.Writer);
    }

    [TestCleanup]
    public void Cleanup()
    {
        fileProviderMock.VerifyAll();
        cloudOrchestrationServiceMock.VerifyAll();
        validationJobStoreMock.VerifyAll();
        mandateServiceMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    public void CreateFileHandleForJob()
    {
        const string originalFileName = "BIZARRESCAN.xtf";
        const string tempFileName = "TEMP.xtf";
        using var expectedFileHandle = new FileHandle(tempFileName, Stream.Null);

        var job = new ValidationJob(Guid.NewGuid(), originalFileName, tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created, DateTime.Now);
        validationJobStoreMock
            .Setup(x => x.GetJob(job.Id))
            .Returns(job);
        fileProviderMock.Setup(x => x.Initialize(job.Id));
        fileProviderMock.Setup(x => x.CreateFileWithRandomName(".xtf")).Returns(expectedFileHandle);

        var actualFileHandle = validationService.CreateFileHandleForJob(job.Id, originalFileName);

        Assert.AreEqual(expectedFileHandle, actualFileHandle);
    }

    [TestMethod]
    public void CreateFileHandleForJobThrowsForUnknownJob()
    {
        var unknownJobId = Guid.NewGuid();
        validationJobStoreMock
            .Setup(x => x.GetJob(unknownJobId))
            .Returns((ValidationJob?)null);

        Assert.ThrowsExactly<ArgumentException>(() => validationService.CreateFileHandleForJob(unknownJobId, "SomeFile.xtf"));
    }

    [TestMethod]
    public async Task StartJobAsyncThrowsForUnknownJob()
    {
        var jobId = Guid.NewGuid();
        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns((ValidationJob?)null);

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
        {
            await validationService.StartJobAsync(jobId, 0, null);
        });
    }

    [TestMethod]
    public async Task StartJobAsyncSuccess()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var pipelineId = "pipeline1";
        var mandate = new Mandate { Id = 1, Name = nameof(StartJobAsyncSuccess), FileTypes = [".xtf"], PipelineId = pipelineId };
        var user = new User { Id = 2, FullName = nameof(StartJobAsyncSuccess) };
        var tempFileName = "file.xtf";
        var tempFilePath = $"path/to/{tempFileName}";
        var originalFileName = "original.xtf";

        var job = new ValidationJob(jobId, originalFileName, tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now);
        var startedJob = new ValidationJob(jobId, originalFileName, tempFileName, mandate.Id, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Processing, DateTime.Now);

        var pipeline = new Mock<IPipeline>(MockBehavior.Strict);

        validationService = new ValidationService(
            validationJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            pipelineFactoryMock.Object);

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, user))
            .ReturnsAsync(mandate);
        validationJobStoreMock
            .Setup(x => x.StartJob(jobId, pipeline.Object, mandate.Id))
            .Returns(startedJob);

        fileProviderMock.Setup(x => x.Initialize(jobId));
        fileProviderMock.Setup(x => x.GetFilePath(tempFileName)).Returns(tempFilePath);

        pipelineFactoryMock.Setup(x => x.CreatePipeline(
            pipelineId,
            It.Is<IPipelineFile>(file => file.OriginalFileName == originalFileName),
            It.IsAny<Guid>()))
            .Returns(pipeline.Object);

        // Act
        var result = await validationService.StartJobAsync(jobId, mandate.Id, user);

        // Assert
        Assert.AreEqual(startedJob, result);
        Assert.AreEqual(mandate.Id, result.MandateId);
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateThrowsForUnsupportedFileType()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var tempFileName = "file.xtf";
        var mandateId = 1;
        var user = new User { Id = 2, FullName = nameof(StartJobAsyncWithMandateThrowsForUnsupportedFileType) };
        var job = new ValidationJob(jobId, "original.xtf", tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now);
        var mandate = new Mandate { Id = mandateId, Name = nameof(StartJobAsyncWithMandateThrowsForUnsupportedFileType), FileTypes = [".csv"] };
        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, user))
            .ReturnsAsync(mandate);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await validationService.StartJobAsync(jobId, mandateId, user);
        });
        Assert.AreEqual($"The job <{jobId}> could not be started with mandate <{mandateId}>.", exception.Message);
    }

    [TestMethod]
    public async Task StartJobAsyncWithMandateThrowsForInvalidMandate()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var tempFileName = "file.xtf";
        var mandateId = 1;
        var user = new User { Id = 2, FullName = nameof(StartJobAsyncWithMandateThrowsForInvalidMandate) };

        var job = new ValidationJob(jobId, "original.xtf", tempFileName, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now);

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, user))
            .ReturnsAsync((Mandate?)null);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await validationService.StartJobAsync(jobId, mandateId, user);
        });

        Assert.AreEqual($"The job <{jobId}> could not be started with mandate <{mandateId}>.", exception.Message);
    }

    [TestMethod]
    public async Task StartJobAsyncQueuesPreflightForCloudJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mandateId = 1;
        var pipelineId = "pipeline1";
        var mandate = new Mandate { Id = mandateId, Name = nameof(StartJobAsyncQueuesPreflightForCloudJob), FileTypes = [".xtf"], PipelineId = pipelineId };
        var user = new User { Id = 2, FullName = nameof(StartJobAsyncQueuesPreflightForCloudJob), AuthIdentifier = "auth-123" };

        var cloudJob = new ValidationJob(jobId, null, null, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created, DateTime.Now, UploadMethod.Cloud, ImmutableList.Create(new CloudFileInfo("test.xtf", "uploads/test.xtf", 1024)));
        var verifyingJob = new ValidationJob(jobId, null, null, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.VerifyingUpload, DateTime.Now, UploadMethod.Cloud);

        validationJobStoreMock.SetupSequence(x => x.GetJob(jobId))
            .Returns(cloudJob)
            .Returns(verifyingJob);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, user)).ReturnsAsync(mandate);
        validationJobStoreMock.Setup(x => x.SetJobStatus(jobId, Status.VerifyingUpload)).Returns(verifyingJob);

        // Act
        var result = await validationService.StartJobAsync(jobId, mandateId, user);

        // Assert
        Assert.AreEqual(Status.VerifyingUpload, result.Status);

        // Verify a PreflightRequest was written to the channel
        Assert.IsTrue(preflightQueue.Reader.TryRead(out var request));
        Assert.AreEqual(jobId, request.JobId);
        Assert.AreEqual(mandateId, request.MandateId);
        Assert.AreEqual("auth-123", request.UserAuthId);

        cloudOrchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(It.IsAny<Guid>()), Times.Never);
        cloudOrchestrationServiceMock.Verify(x => x.StageFilesLocallyAsync(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task StartJobAsyncDoesNotRunPreflightForDirectUploadJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mandate = new Mandate { Id = 1, Name = nameof(StartJobAsyncDoesNotRunPreflightForDirectUploadJob), FileTypes = [".xtf"] };
        var user = new User { Id = 2, FullName = nameof(StartJobAsyncDoesNotRunPreflightForDirectUploadJob) };

        var pipelineId = "pipeline1";
        mandate.PipelineId = pipelineId;
        var directJob = new ValidationJob(jobId, "original.xtf", "file.xtf", null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now);
        var startedJob = new ValidationJob(jobId, "original.xtf", "file.xtf", mandate.Id, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Processing, DateTime.Now);
        var tempFilePath = $"path/to/{directJob.TempFileName}";
        var pipeline = new Mock<IPipeline>(MockBehavior.Strict);

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(directJob);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, user)).ReturnsAsync(mandate);

        fileProviderMock.Setup(x => x.Initialize(jobId));
        fileProviderMock.Setup(x => x.GetFilePath(directJob.TempFileName!)).Returns(tempFilePath);

        pipelineFactoryMock.Setup(x => x.CreatePipeline(pipelineId, It.IsAny<IPipelineFile>(), It.IsAny<Guid>()))
            .Returns(pipeline.Object);

        validationJobStoreMock
            .Setup(x => x.StartJob(jobId, pipeline.Object, mandate.Id))
            .Returns(startedJob);

        // Act
        var result = await validationService.StartJobAsync(jobId, mandate.Id, user);

        // Assert
        Assert.AreEqual(startedJob, result);
        cloudOrchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(It.IsAny<Guid>()), Times.Never);
        cloudOrchestrationServiceMock.Verify(x => x.StageFilesLocallyAsync(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task StartJobAsyncValidatesMandateBeforeQueuingCloudJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mandateId = 1;
        var cloudJob = new ValidationJob(jobId, null, null, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created, DateTime.Now, UploadMethod.Cloud, ImmutableList.Create(new CloudFileInfo("test.xtf", "uploads/test.xtf", 1024)));

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(cloudJob);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, It.IsAny<User?>())).ReturnsAsync((Mandate?)null);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await validationService.StartJobAsync(jobId, mandateId, null);
        });

        Assert.AreEqual($"The job <{jobId}> could not be started with mandate <{mandateId}>.", ex.Message);

        // Verify nothing was queued
        Assert.IsFalse(preflightQueue.Reader.TryRead(out _));
    }

    [TestMethod]
    public async Task StartJobAsyncThrowsWhenCloudStorageDisabled()
    {
        // Arrange — service without cloud dependencies
        var serviceWithoutCloud = new ValidationService(
            validationJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            pipelineFactoryMock.Object);

        var jobId = Guid.NewGuid();
        var cloudJob = new ValidationJob(jobId, null, null, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created, DateTime.Now, UploadMethod.Cloud, ImmutableList.Create(new CloudFileInfo("test.xtf", "uploads/test.xtf", 1024)));

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(cloudJob);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await serviceWithoutCloud.StartJobAsync(jobId, 1, null);
        });

        Assert.AreEqual("Cloud storage is not enabled.", ex.Message);
    }
}
