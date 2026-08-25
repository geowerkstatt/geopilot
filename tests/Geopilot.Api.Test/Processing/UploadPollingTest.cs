using Geopilot.Api.Enums;
using Geopilot.Api.Exceptions;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Immutable;
using System.Threading.Channels;

namespace Geopilot.Api.Test.Processing;

[TestClass]
public class UploadPollingTest
{
    private ProcessingJobStore jobStore;
    private UploadStore uploadStore;
    private Channel<PreflightRequest> preflightChannel;
    private ProcessingService processingService;
    private PreflightBackgroundService backgroundService;

    private Mock<IUploadOrchestrationService> orchestrationServiceMock;
    private Mock<IUploadStorage> uploadStorageMock;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IPipelineFactory> pipelineFactoryMock;
    private Context context;

    [TestInitialize]
    public void Initialize()
    {
        orchestrationServiceMock = new Mock<IUploadOrchestrationService>(MockBehavior.Strict);
        uploadStorageMock = new Mock<IUploadStorage>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        pipelineFactoryMock = new Mock<IPipelineFactory>(MockBehavior.Strict);
        context = AssemblyInitialize.DbFixture.GetTestContext();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        jobStore = new ProcessingJobStore();
        uploadStore = new UploadStore();
        preflightChannel = Channel.CreateUnbounded<PreflightRequest>();

        processingService = new ProcessingService(
            jobStore,
            uploadStore,
            mandateServiceMock.Object,
            pipelineFactoryMock.Object,
            preflightChannel.Writer);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IProcessingJobStore))).Returns(jobStore);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUploadStore))).Returns(uploadStore);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUploadOrchestrationService))).Returns(orchestrationServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUploadStorage))).Returns(uploadStorageMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IMandateService))).Returns(mandateServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IPipelineFactory))).Returns(pipelineFactoryMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(Context))).Returns(context);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        backgroundService = new PreflightBackgroundService(
            preflightChannel.Reader,
            scopeFactoryMock.Object,
            new Mock<ILogger<PreflightBackgroundService>>().Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        context.Dispose();
    }

    [TestMethod]
    public async Task PollReturnsVerifyingUploadBeforePreflightCompletes()
    {
        var (jobId, _, _, _) = await CreateAndStartJobAsync();

        var polledJob = processingService.GetJob(jobId);

        Assert.IsNotNull(polledJob);
        Assert.AreNotEqual(ProcessingState.Failed, polledJob.State);
    }

    [TestMethod]
    public async Task PollReturnsProcessingAfterSuccessfulPreflight()
    {
        var (jobId, uploadId, mandate, user) = await CreateAndStartJobAsync();

        SetupSuccessfulPreflight(jobId, uploadId, mandate, user);

        var request = await preflightChannel.Reader.ReadAsync();
        await backgroundService.ProcessRequestAsync(request);

        var polledJob = processingService.GetJob(jobId);

        Assert.IsNotNull(polledJob);
        Assert.AreNotEqual(ProcessingState.Failed, polledJob.State);
        Assert.AreEqual(mandate.Id, polledJob.MandateId);
    }

    [TestMethod]
    [DataRow(PreflightFailureReason.IncompleteUpload)]
    [DataRow(PreflightFailureReason.ThreatDetected)]
    [DataRow(PreflightFailureReason.SizeExceeded)]
    public async Task PollReturnsFailedAfterPreflightFailure(PreflightFailureReason reason)
    {
        var (jobId, uploadId, _, _) = await CreateAndStartJobAsync();

        orchestrationServiceMock
            .Setup(x => x.RunPreflightChecksAsync(uploadId))
            .ThrowsAsync(new UploadPreflightException(reason, "Preflight check failed."));
        orchestrationServiceMock
            .Setup(x => x.ReleaseUploadAsync(uploadId))
            .Returns(Task.CompletedTask);

        var request = await preflightChannel.Reader.ReadAsync();
        await backgroundService.ProcessRequestAsync(request);

        var polledJob = processingService.GetJob(jobId);

        Assert.IsNotNull(polledJob);
        Assert.AreEqual(ProcessingState.Failed, polledJob.State);
    }

    [TestMethod]
    public async Task PollReturnsFailedAfterGenericException()
    {
        var (jobId, uploadId, _, _) = await CreateAndStartJobAsync();

        orchestrationServiceMock
            .Setup(x => x.RunPreflightChecksAsync(uploadId))
            .ThrowsAsync(new InvalidOperationException("Unexpected error during processing."));
        orchestrationServiceMock
            .Setup(x => x.ReleaseUploadAsync(uploadId))
            .Returns(Task.CompletedTask);

        var request = await preflightChannel.Reader.ReadAsync();
        await backgroundService.ProcessRequestAsync(request);

        var polledJob = processingService.GetJob(jobId);

        Assert.IsNotNull(polledJob);
        Assert.AreEqual(ProcessingState.Failed, polledJob.State);
    }

    [TestMethod]
    public async Task PollReturnsFailedEvenWhenCleanupFails()
    {
        var (jobId, uploadId, _, _) = await CreateAndStartJobAsync();

        orchestrationServiceMock
            .Setup(x => x.RunPreflightChecksAsync(uploadId))
            .ThrowsAsync(new UploadPreflightException(PreflightFailureReason.ThreatDetected, "Threat detected."));
        orchestrationServiceMock
            .Setup(x => x.ReleaseUploadAsync(uploadId))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable."));

        var request = await preflightChannel.Reader.ReadAsync();
        await backgroundService.ProcessRequestAsync(request);

        var polledJob = processingService.GetJob(jobId);

        Assert.IsNotNull(polledJob);
        Assert.AreEqual(ProcessingState.Failed, polledJob.State);
    }

    private async Task<(Guid JobId, Guid UploadId, Mandate Mandate, User User)> CreateAndStartJobAsync()
    {
        var pipelineId = "pipeline1";
        var mandate = new Mandate { Id = 1, Name = TestHelpers.Localized("Test Mandate"), PipelineId = pipelineId };
        var user = new User { FullName = "Test User", AuthIdentifier = "auth-123" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var uploadId = Guid.NewGuid();
        var uploadedFiles = ImmutableList.Create(new UploadedFileInfo("test.xtf", "uploads/test.xtf", 1024));
        uploadStore.CreateUpload(uploadId, uploadedFiles);

        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, user)).ReturnsAsync(mandate);

        // The pipeline is now instantiated up front in StartJobAsync (without files) and attached to the job.
        // On a preflight failure the service disposes the attached pipeline, so allow Dispose on the strict mock.
        var pipeline = new Mock<IPipeline>(MockBehavior.Strict);
        pipeline.SetupGet(p => p.Id).Returns(pipelineId);
        pipeline.Setup(p => p.Dispose());
        pipelineFactoryMock.Setup(x => x.CreatePipeline(pipelineId, It.IsAny<Guid>())).Returns(pipeline.Object);

        var job = await processingService.StartJobAsync(uploadId, mandate.Id, user);

        return (job.Id, uploadId, mandate, user);
    }

    private void SetupSuccessfulPreflight(Guid jobId, Guid uploadId, Mandate mandate, User user)
    {
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId)).Returns(Task.CompletedTask);
        orchestrationServiceMock.Setup(x => x.RegisterJobFiles(uploadId, jobId))
            .Returns(() => new List<IPipelineFile> { new Mock<IPipelineFile>().Object });
    }
}
