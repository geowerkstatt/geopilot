using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
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
    private Channel<PreflightRequest> preflightQueue;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        processingJobStoreMock = new Mock<IProcessingJobStore>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        uploadStoreMock = new Mock<IUploadStore>(MockBehavior.Strict);
        preflightQueue = Channel.CreateUnbounded<PreflightRequest>();

        processingService = new ProcessingService(
            processingJobStoreMock.Object,
            uploadStoreMock.Object,
            mandateServiceMock.Object,
            preflightQueue.Writer);
    }

    [TestCleanup]
    public void Cleanup()
    {
        processingJobStoreMock.VerifyAll();
        mandateServiceMock.VerifyAll();
        uploadStoreMock.VerifyAll();
        context.Dispose();
    }

    [TestMethod]
    public async Task StartJobThrowsForUnknownUpload()
    {
        var uploadId = Guid.NewGuid();
        uploadStoreMock.Setup(x => x.GetUpload(uploadId)).Returns((UploadInfo?)null);

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
        {
            await processingService.StartJob(uploadId, 1, null);
        });

        Assert.AreEqual("uploadId", exception.ParamName);
    }

    [TestMethod]
    public async Task StartJobSuccessSetsPipelineIdAndQueuesPreflight()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var pipelineId = "pipeline1";
        var mandate = new Mandate { Id = 1, Name = nameof(StartJobSuccessSetsPipelineIdAndQueuesPreflight), FileTypes = [".xtf"], PipelineId = pipelineId };
        var user = new User { Id = 2, FullName = nameof(StartJobSuccessSetsPipelineIdAndQueuesPreflight), AuthIdentifier = "auth-123" };

        var upload = new UploadInfo(uploadId, ImmutableList.Create(new CloudFileInfo("test.xtf", "uploads/test.xtf", 1024)), DateTime.Now);
        var job = new ProcessingJob(jobId, new List<ProcessingJobFile>(), null, DateTime.Now);

        uploadStoreMock.Setup(x => x.GetUpload(uploadId)).Returns(upload);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, user)).ReturnsAsync(mandate);
        processingJobStoreMock.Setup(x => x.CreateJob()).Returns(job);
        processingJobStoreMock.Setup(x => x.SetPipelineId(jobId, pipelineId)).Returns(job);
        processingJobStoreMock.Setup(x => x.GetJob(jobId)).Returns(job);

        // Act
        var result = await processingService.StartJob(uploadId, mandate.Id, user);

        // Assert
        Assert.AreEqual(job, result);
        processingJobStoreMock.Verify(x => x.SetPipelineId(jobId, pipelineId), Times.Once);

        Assert.IsTrue(preflightQueue.Reader.TryRead(out var request));
        Assert.AreEqual(jobId, request.JobId);
        Assert.AreEqual(uploadId, request.UploadId);
        Assert.AreEqual(mandate.Id, request.MandateId);
        Assert.AreEqual("auth-123", request.UserAuthId);
    }

    [TestMethod]
    public async Task StartJobThrowsForInvalidMandate()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var mandateId = 1;
        var user = new User { Id = 2, FullName = nameof(StartJobThrowsForInvalidMandate) };

        var upload = new UploadInfo(uploadId, ImmutableList.Create(new CloudFileInfo("test.xtf", "uploads/test.xtf", 1024)), DateTime.Now);

        uploadStoreMock.Setup(x => x.GetUpload(uploadId)).Returns(upload);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, user)).ReturnsAsync((Mandate?)null);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await processingService.StartJob(uploadId, mandateId, user);
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

        var upload = new UploadInfo(uploadId, ImmutableList.Create(new CloudFileInfo("test.xtf", "uploads/test.xtf", 1024)), DateTime.Now);
        var mandate = new Mandate { Id = mandateId, Name = nameof(StartJobThrowsForMandateWithoutPipeline), FileTypes = [".xtf"], PipelineId = null };

        uploadStoreMock.Setup(x => x.GetUpload(uploadId)).Returns(upload);
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandateId, user)).ReturnsAsync(mandate);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await processingService.StartJob(uploadId, mandateId, user);
        });

        Assert.AreEqual($"The upload <{uploadId}> could not be started with mandate <{mandateId}>.", exception.Message);

        Assert.IsFalse(preflightQueue.Reader.TryRead(out _));
    }
}
