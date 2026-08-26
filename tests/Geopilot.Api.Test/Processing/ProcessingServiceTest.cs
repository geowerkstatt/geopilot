using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Moq;
using System.Collections.Immutable;
using System.Threading.Channels;

namespace Geopilot.Api.Test.Processing;

[TestClass]
public class ProcessingServiceTest
{
    private Context context;
    private ProcessingService processingService;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IProcessingJobStore> processingJobStoreMock;
    private Mock<IUploadStore> uploadStoreMock;
    private Mock<IPipelineFactory> pipelineFactoryMock;
    private Mock<IPipelineRunRecorder> runRecorderMock;
    private Channel<PreflightRequest> preflightQueue;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        processingJobStoreMock = new Mock<IProcessingJobStore>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        uploadStoreMock = new Mock<IUploadStore>(MockBehavior.Strict);
        pipelineFactoryMock = new Mock<IPipelineFactory>(MockBehavior.Strict);
        runRecorderMock = new Mock<IPipelineRunRecorder>();
        preflightQueue = Channel.CreateUnbounded<PreflightRequest>();

        processingService = new ProcessingService(
            processingJobStoreMock.Object,
            uploadStoreMock.Object,
            mandateServiceMock.Object,
            pipelineFactoryMock.Object,
            runRecorderMock.Object,
            preflightQueue.Writer);
    }

    [TestCleanup]
    public void Cleanup()
    {
        processingJobStoreMock.VerifyAll();
        mandateServiceMock.VerifyAll();
        uploadStoreMock.VerifyAll();
        pipelineFactoryMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    public async Task StartJobThrowsForUnknownUpload()
    {
        var uploadId = Guid.NewGuid();
        uploadStoreMock.Setup(x => x.GetUpload(uploadId)).Returns((UploadInfo?)null);

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
        {
            await processingService.StartJobAsync(uploadId, 1, null);
        });

        Assert.AreEqual("uploadId", exception.ParamName);
    }

    [TestMethod]
    public async Task StartJobSuccessAttachesPipelineAndQueuesPreflight()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var pipelineId = "pipeline1";
        var mandate = new Mandate { Id = 1, Name = TestHelpers.Localized(nameof(StartJobSuccessAttachesPipelineAndQueuesPreflight)), FileTypes = [".xtf"], PipelineId = pipelineId };
        var user = new User { Id = 2, FullName = nameof(StartJobSuccessAttachesPipelineAndQueuesPreflight), AuthIdentifier = "auth-123" };

        var upload = new UploadInfo(uploadId, ImmutableList.Create(new UploadedFileInfo("test.xtf", "uploads/test.xtf", 1024)), DateTime.Now);
        var job = new ProcessingJob(jobId, Guid.NewGuid(), null, DateTime.Now);
        var pipeline = new Mock<IPipeline>().Object;

        uploadStoreMock.Setup(x => x.GetUpload(uploadId)).Returns(upload);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, user)).ReturnsAsync(mandate);
        processingJobStoreMock.Setup(x => x.CreateJob(uploadId)).Returns(job);
        pipelineFactoryMock.Setup(x => x.CreatePipeline(pipelineId, jobId)).Returns(pipeline);
        processingJobStoreMock.Setup(x => x.AttachPipeline(jobId, pipeline, mandate.Id)).Returns(job);
        processingJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);

        // Act
        var result = await processingService.StartJobAsync(uploadId, mandate.Id, user);

        // Assert
        Assert.AreEqual(job, result);
        processingJobStoreMock.Verify(x => x.CreateJob(uploadId), Times.Once);
        pipelineFactoryMock.Verify(x => x.CreatePipeline(pipelineId, jobId), Times.Once);
        processingJobStoreMock.Verify(x => x.AttachPipeline(jobId, pipeline, mandate.Id), Times.Once);

        Assert.IsTrue(preflightQueue.Reader.TryRead(out var request));
        Assert.AreEqual(jobId, request.JobId);
        Assert.AreEqual(uploadId, request.UploadId);
        runRecorderMock.Verify(r => r.RecordJobStartedAsync(job, mandate, user, upload), Times.Once);
    }

    [TestMethod]
    public async Task StartJobRemovesJobWhenProtocolRecordCannotBeWritten()
    {
        // The start record is deliberately hard: without it there must be no job and no 202.
        var uploadId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var pipelineId = "pipeline1";
        var mandate = new Mandate { Id = 1, Name = TestHelpers.Localized(nameof(StartJobRemovesJobWhenProtocolRecordCannotBeWritten)), FileTypes = [".xtf"], PipelineId = pipelineId };
        var user = new User { Id = 2, FullName = nameof(StartJobRemovesJobWhenProtocolRecordCannotBeWritten), AuthIdentifier = "auth-456" };

        var upload = new UploadInfo(uploadId, ImmutableList.Create(new UploadedFileInfo("test.xtf", "uploads/test.xtf", 1024)), DateTime.Now);
        var job = new ProcessingJob(jobId, uploadId, null, DateTime.Now);
        var pipeline = new Mock<IPipeline>().Object;

        uploadStoreMock.Setup(x => x.GetUpload(uploadId)).Returns(upload);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, user)).ReturnsAsync(mandate);
        processingJobStoreMock.Setup(x => x.CreateJob(uploadId)).Returns(job);
        pipelineFactoryMock.Setup(x => x.CreatePipeline(pipelineId, jobId)).Returns(pipeline);
        processingJobStoreMock.Setup(x => x.AttachPipeline(jobId, pipeline, mandate.Id)).Returns(job);
        processingJobStoreMock.Setup(x => x.RemoveJob(jobId)).Returns(true);
        runRecorderMock
            .Setup(r => r.RecordJobStartedAsync(job, mandate, user, upload))
            .ThrowsAsync(new InvalidOperationException("protocol database down"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await processingService.StartJobAsync(uploadId, mandate.Id, user);
        });

        processingJobStoreMock.Verify(x => x.RemoveJob(jobId), Times.Once, "a job without protocol record must not stay in the store.");
        Assert.IsFalse(preflightQueue.Reader.TryRead(out _), "a job without protocol record must not be queued.");
    }

    [TestMethod]
    public async Task StartJobThrowsForInvalidMandate()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var mandateId = 1;
        var user = new User { Id = 2, FullName = nameof(StartJobThrowsForInvalidMandate) };

        var upload = new UploadInfo(uploadId, ImmutableList.Create(new UploadedFileInfo("test.xtf", "uploads/test.xtf", 1024)), DateTime.Now);

        uploadStoreMock.Setup(x => x.GetUpload(uploadId)).Returns(upload);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, user)).ReturnsAsync((Mandate?)null);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await processingService.StartJobAsync(uploadId, mandateId, user);
        });

        Assert.AreEqual($"The upload <{uploadId}> could not be started with mandate <{mandateId}>.", exception.Message);

        // Nothing should have been queued and no job created.
        Assert.IsFalse(preflightQueue.Reader.TryRead(out _));
    }

    [TestMethod]
    public async Task StartJobThrowsForMandateWithoutPipeline()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var mandateId = 1;
        var user = new User { Id = 2, FullName = nameof(StartJobThrowsForMandateWithoutPipeline) };

        var upload = new UploadInfo(uploadId, ImmutableList.Create(new UploadedFileInfo("test.xtf", "uploads/test.xtf", 1024)), DateTime.Now);
        var mandate = new Mandate { Id = mandateId, Name = TestHelpers.Localized(nameof(StartJobThrowsForMandateWithoutPipeline)), FileTypes = [".xtf"], PipelineId = null };

        uploadStoreMock.Setup(x => x.GetUpload(uploadId)).Returns(upload);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, user)).ReturnsAsync(mandate);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await processingService.StartJobAsync(uploadId, mandateId, user);
        });

        Assert.AreEqual($"The upload <{uploadId}> could not be started with mandate <{mandateId}>.", exception.Message);

        Assert.IsFalse(preflightQueue.Reader.TryRead(out _));
    }
}
