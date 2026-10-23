using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private Channel<PreflightRequest> preflightQueue;

    [TestInitialize]
    public void Initialize()
    {
        context = AssemblyInitialize.DbFixture.GetTestContext();
        processingJobStoreMock = new Mock<IProcessingJobStore>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        uploadStoreMock = new Mock<IUploadStore>(MockBehavior.Strict);
        pipelineFactoryMock = new Mock<IPipelineFactory>(MockBehavior.Strict);
        preflightQueue = Channel.CreateUnbounded<PreflightRequest>();

        // The start itself is covered by JobLifecycleTest; these tests only cover what the service refuses.
        var lifecycle = new JobLifecycle(
            Mock.Of<ILogger<JobLifecycle>>(),
            processingJobStoreMock.Object,
            pipelineFactoryMock.Object,
            preflightQueue.Writer,
            Mock.Of<IServiceScopeFactory>(),
            Options.Create(new ProcessingOptions { JobTimeout = TimeSpan.FromMinutes(5) }));

        processingService = new ProcessingService(
            processingJobStoreMock.Object,
            uploadStoreMock.Object,
            mandateServiceMock.Object,
            lifecycle);
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
            await processingService.StartJobAsync(uploadId, 1, null, null);
        });

        Assert.AreEqual("uploadId", exception.ParamName);
    }

    [TestMethod]
    public async Task StartJobThrowsForInvalidMandate()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var mandateId = 1;
        var declarer = Declarer.ForUser(2);

        var upload = new UploadInfo(uploadId, ImmutableList.Create(new UploadedFileInfo("test.xtf", "uploads/test.xtf", 1024)), DateTime.Now);

        uploadStoreMock.Setup(x => x.GetUpload(uploadId)).Returns(upload);
        mandateServiceMock.Setup(x => x.GetMandateForDeclarerAsync(mandateId, declarer)).ReturnsAsync((Mandate?)null);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await processingService.StartJobAsync(uploadId, mandateId, declarer, null);
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
        var declarer = Declarer.ForUser(2);

        var upload = new UploadInfo(uploadId, ImmutableList.Create(new UploadedFileInfo("test.xtf", "uploads/test.xtf", 1024)), DateTime.Now);
        var mandate = new Mandate { Id = mandateId, Name = TestHelpers.Localized(nameof(StartJobThrowsForMandateWithoutPipeline)), FileTypes = [".xtf"], PipelineId = null };

        uploadStoreMock.Setup(x => x.GetUpload(uploadId)).Returns(upload);
        mandateServiceMock.Setup(x => x.GetMandateForDeclarerAsync(mandateId, declarer)).ReturnsAsync(mandate);

        // Act & Assert
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await processingService.StartJobAsync(uploadId, mandateId, declarer, null);
        });

        Assert.AreEqual($"The upload <{uploadId}> could not be started with mandate <{mandateId}>.", exception.Message);

        Assert.IsFalse(preflightQueue.Reader.TryRead(out _));
    }
}
