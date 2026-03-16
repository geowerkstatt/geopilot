using Geopilot.Api.Enums;
using Geopilot.Api.Exceptions;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Services;
using Geopilot.Api.Validation;
using Geopilot.PipelineCore.Pipeline;
using Moq;
using System.Collections.Immutable;

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

    [TestInitialize]
    public void Initialize()
    {
        fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);
        cloudOrchestrationServiceMock = new Mock<ICloudOrchestrationService>(MockBehavior.Strict);
        context = AssemblyInitialize.DbFixture.GetTestContext();
        validationJobStoreMock = new Mock<IValidationJobStore>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        pipelineFactoryMock = new Mock<IPipelineFactory>(MockBehavior.Strict);

        validationService = new ValidationService(
            validationJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            pipelineFactoryMock.Object,
            cloudOrchestrationServiceMock.Object);
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
            It.Is<IPipelineTransferFile>(file => file.OriginalFileName == originalFileName && file.FileName == tempFileName && file.FilePath == tempFilePath),
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
    public async Task StartJobAsyncRunsPreflightAndStagingForCloudJob()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var mandate = new Mandate { Id = 1, Name = nameof(StartJobAsyncRunsPreflightAndStagingForCloudJob), FileTypes = [".xtf"] };
        var user = new User { Id = 2, FullName = nameof(StartJobAsyncRunsPreflightAndStagingForCloudJob) };

        var cloudJob = new ValidationJob(jobId, null, null, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created, DateTime.Now, UploadMethod.Cloud, ImmutableList.Create(new CloudFileInfo("test.xtf", "uploads/test.xtf", 1024)));
        var stagedJob = new ValidationJob(jobId, "test.xtf", "random.xtf", null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Ready, DateTime.Now, UploadMethod.Cloud);
        var startedJob = new ValidationJob(jobId, "test.xtf", "random.xtf", mandate.Id, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Processing, DateTime.Now, UploadMethod.Cloud);

        var pipelineId = "pipeline1";
        mandate.PipelineId = pipelineId;
        var tempFilePath = $"path/to/{stagedJob.TempFileName}";
        var pipeline = new Mock<IPipeline>(MockBehavior.Strict);

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(cloudJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(jobId)).Returns(Task.CompletedTask);
        cloudOrchestrationServiceMock.Setup(x => x.StageFilesLocallyAsync(jobId)).ReturnsAsync(stagedJob);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, user)).ReturnsAsync(mandate);

        fileProviderMock.Setup(x => x.Initialize(jobId));
        fileProviderMock.Setup(x => x.GetFilePath(stagedJob.TempFileName!)).Returns(tempFilePath);

        pipelineFactoryMock.Setup(x => x.CreatePipeline(pipelineId, It.IsAny<IPipelineTransferFile>(), It.IsAny<Guid>()))
            .Returns(pipeline.Object);

        validationJobStoreMock
            .Setup(x => x.StartJob(jobId, pipeline.Object, mandate.Id))
            .Returns(startedJob);

        // Act
        var result = await validationService.StartJobAsync(jobId, mandate.Id, user);

        // Assert
        Assert.AreEqual(startedJob, result);
        Assert.AreEqual(Status.Processing, result.Status);
        cloudOrchestrationServiceMock.Verify(x => x.RunPreflightChecksAsync(jobId), Times.Once);
        cloudOrchestrationServiceMock.Verify(x => x.StageFilesLocallyAsync(jobId), Times.Once);
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

        pipelineFactoryMock.Setup(x => x.CreatePipeline(pipelineId, It.IsAny<IPipelineTransferFile>(), It.IsAny<Guid>()))
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
    public async Task StartJobAsyncPropagatesPreflightException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var cloudJob = new ValidationJob(jobId, null, null, null, ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created, DateTime.Now, UploadMethod.Cloud, ImmutableList.Create(new CloudFileInfo("test.xtf", "uploads/test.xtf", 1024)));

        validationJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(cloudJob);
        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(jobId))
            .ThrowsAsync(new CloudUploadPreflightException(PreflightFailureReason.IncompleteUpload, "File 'test.xtf' was not uploaded."));

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<CloudUploadPreflightException>(async () =>
        {
            await validationService.StartJobAsync(jobId, 1, null);
        });

        Assert.AreEqual(PreflightFailureReason.IncompleteUpload, ex.FailureReason);
    }
}
