using Geopilot.Api.Enums;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Services;
using Geopilot.Api.Processing;
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
    private ProcessingService ProcessingService;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IProcessingJobStore> ProcessingJobStoreMock;
    private Mock<IPipelineFactory> pipelineFactoryMock;
    private Channel<PreflightRequest> preflightQueue;

    [TestInitialize]
    public void Initialize()
    {
        fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);
        cloudOrchestrationServiceMock = new Mock<ICloudOrchestrationService>(MockBehavior.Strict);
        context = AssemblyInitialize.DbFixture.GetTestContext();
        ProcessingJobStoreMock = new Mock<IProcessingJobStore>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        pipelineFactoryMock = new Mock<IPipelineFactory>(MockBehavior.Strict);
        preflightQueue = Channel.CreateUnbounded<PreflightRequest>();

        ProcessingService = new ProcessingService(
            ProcessingJobStoreMock.Object,
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
        ProcessingJobStoreMock.VerifyAll();
        mandateServiceMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    public void CreateFileHandleForJob()
    {
        const string originalFileName = "BIZARRESCAN.xtf";
        const string tempFileName = "TEMP.xtf";
        using var expectedFileHandle = new FileHandle(tempFileName, Stream.Null);

        var job = new ProcessingJob(Guid.NewGuid(), new List<ProcessingJobFile>() { new ProcessingJobFile(originalFileName, tempFileName) }, null, DateTime.Now);
        ProcessingJobStoreMock
            .Setup(x => x.GetJob(job.Id))
            .Returns(job);
        fileProviderMock.Setup(x => x.Initialize(job.Id));
        fileProviderMock.Setup(x => x.CreateFileWithRandomName(".xtf")).Returns(expectedFileHandle);

        var actualFileHandle = ProcessingService.CreateFileHandleForJob(job.Id, originalFileName);

        Assert.AreEqual(expectedFileHandle, actualFileHandle);
    }

    [TestMethod]
    public void CreateFileHandleForJobThrowsForUnknownJob()
    {
        var unknownJobId = Guid.NewGuid();
        ProcessingJobStoreMock
            .Setup(x => x.GetJob(unknownJobId))
            .Returns((ProcessingJob?)null);

        Assert.ThrowsExactly<ArgumentException>(() => ProcessingService.CreateFileHandleForJob(unknownJobId, "SomeFile.xtf"));
    }

    [TestMethod]
    public async Task StartJobAsyncThrowsForUnknownJob()
    {
        var jobId = Guid.NewGuid();
        ProcessingJobStoreMock.Setup(x => x.GetJob(jobId)).Returns((ProcessingJob?)null);

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
        {
            await ProcessingService.StartJobAsync(jobId, 0, null);
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

        var job = new ProcessingJob(jobId, new List<ProcessingJobFile>() { new ProcessingJobFile(originalFileName, tempFileName) }, null, DateTime.Now);
        var startedJob = new ProcessingJob(jobId, new List<ProcessingJobFile>() { new ProcessingJobFile(originalFileName, tempFileName) }, mandate.Id, DateTime.Now);

        var pipeline = new Mock<IPipeline>(MockBehavior.Strict);

        ProcessingService = new ProcessingService(
            ProcessingJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            pipelineFactoryMock.Object);

        ProcessingJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, user))
            .ReturnsAsync(mandate);
        ProcessingJobStoreMock
            .Setup(x => x.StartJob(jobId, pipeline.Object, mandate.Id))
            .Returns(startedJob);

        fileProviderMock.Setup(x => x.Initialize(jobId));
        fileProviderMock.Setup(x => x.GetFilePath(tempFileName)).Returns(tempFilePath);

        pipelineFactoryMock.Setup(x => x.CreatePipeline(
            pipelineId,
            It.Is<PipelineFileList>(files => files.Files.Any(file => file.OriginalFileName == originalFileName)),
            It.IsAny<Guid>()))
            .Returns(pipeline.Object);

        // Act
        var result = await ProcessingService.StartJobAsync(jobId, mandate.Id, user);

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
        var job = new ProcessingJob(jobId, new List<ProcessingJobFile>() { new ProcessingJobFile("original.xtf", tempFileName) }, null, DateTime.Now);
        var mandate = new Mandate { Id = mandateId, Name = nameof(StartJobAsyncWithMandateThrowsForUnsupportedFileType), FileTypes = [".csv"] };
        ProcessingJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, user))
            .ReturnsAsync(mandate);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await ProcessingService.StartJobAsync(jobId, mandateId, user);
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

        var job = new ProcessingJob(jobId, new List<ProcessingJobFile>() { new ProcessingJobFile("original.xtf", tempFileName) }, null, DateTime.Now);

        ProcessingJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, user))
            .ReturnsAsync((Mandate?)null);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await ProcessingService.StartJobAsync(jobId, mandateId, user);
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

        var cloudJob = new ProcessingJob(jobId, new List<ProcessingJobFile>(), null, DateTime.Now, UploadMethod.Cloud, ImmutableList.Create(new CloudFileInfo("test.xtf", "uploads/test.xtf", 1024)));
        var verifyingJob = new ProcessingJob(jobId, new List<ProcessingJobFile>(), null, DateTime.Now, UploadMethod.Cloud);

        ProcessingJobStoreMock.SetupSequence(x => x.GetJob(jobId))
            .Returns(cloudJob)
            .Returns(verifyingJob);
        ProcessingJobStoreMock.Setup(x => x.SetPipelineId(jobId, pipelineId)).Returns(verifyingJob);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, user)).ReturnsAsync(mandate);

        // Act
        var result = await ProcessingService.StartJobAsync(jobId, mandateId, user);

        // Assert
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
        var directJob = new ProcessingJob(jobId, new List<ProcessingJobFile>() { new ProcessingJobFile("original.xtf", "file.xtf") }, null, DateTime.Now);
        var startedJob = new ProcessingJob(jobId, new List<ProcessingJobFile>() { new ProcessingJobFile("original.xtf", "file.xtf") }, mandate.Id, DateTime.Now);
        var tempFilePath = $"path/to/file.xtf";
        var pipeline = new Mock<IPipeline>(MockBehavior.Strict);

        ProcessingJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(directJob);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, user)).ReturnsAsync(mandate);

        fileProviderMock.Setup(x => x.Initialize(jobId));
        fileProviderMock.Setup(x => x.GetFilePath("file.xtf")).Returns(tempFilePath);

        pipelineFactoryMock.Setup(x => x.CreatePipeline(pipelineId, It.IsAny<PipelineFileList>(), It.IsAny<Guid>()))
            .Returns(pipeline.Object);

        ProcessingJobStoreMock
            .Setup(x => x.StartJob(jobId, pipeline.Object, mandate.Id))
            .Returns(startedJob);

        // Act
        var result = await ProcessingService.StartJobAsync(jobId, mandate.Id, user);

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
        var cloudJob = new ProcessingJob(jobId, new List<ProcessingJobFile>(), null, DateTime.Now, UploadMethod.Cloud, ImmutableList.Create(new CloudFileInfo("test.xtf", "uploads/test.xtf", 1024)));

        ProcessingJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(cloudJob);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, It.IsAny<User?>())).ReturnsAsync((Mandate?)null);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await ProcessingService.StartJobAsync(jobId, mandateId, null);
        });

        Assert.AreEqual($"The job <{jobId}> could not be started with mandate <{mandateId}>.", ex.Message);

        // Verify nothing was queued
        Assert.IsFalse(preflightQueue.Reader.TryRead(out _));
    }

    [TestMethod]
    public async Task StartJobAsyncThrowsWhenCloudStorageDisabled()
    {
        // Arrange — service without cloud dependencies
        var serviceWithoutCloud = new ProcessingService(
            ProcessingJobStoreMock.Object,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            pipelineFactoryMock.Object);

        var jobId = Guid.NewGuid();
        var cloudJob = new ProcessingJob(jobId, new List<ProcessingJobFile>(), null, DateTime.Now, UploadMethod.Cloud, ImmutableList.Create(new CloudFileInfo("test.xtf", "uploads/test.xtf", 1024)));

        ProcessingJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(cloudJob);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await serviceWithoutCloud.StartJobAsync(jobId, 1, null);
        });

        Assert.AreEqual("Cloud storage is not enabled.", ex.Message);
    }
}
