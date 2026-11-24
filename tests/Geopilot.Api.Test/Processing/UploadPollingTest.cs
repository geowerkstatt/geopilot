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
using Microsoft.Extensions.Options;
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
    private Mock<IRecorder> runRecorderMock;
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
        uploadStore = new UploadStore(context);
        preflightChannel = Channel.CreateUnbounded<PreflightRequest>();

        runRecorderMock = new Mock<IRecorder>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IProcessingJobStore))).Returns(jobStore);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUploadStore))).Returns(uploadStore);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUploadOrchestrationService))).Returns(orchestrationServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IRecorder))).Returns(runRecorderMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IUploadStorage))).Returns(uploadStorageMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IMandateService))).Returns(mandateServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IPipelineFactory))).Returns(pipelineFactoryMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(Context))).Returns(context);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var lifecycle = new JobLifecycle(Mock.Of<ILogger<JobLifecycle>>(), jobStore, pipelineFactoryMock.Object, preflightChannel.Writer, scopeFactoryMock.Object, Options.Create(new ProcessingOptions { JobTimeout = TimeSpan.FromMinutes(5) }));

        processingService = new ProcessingService(jobStore, uploadStore, mandateServiceMock.Object, lifecycle);

        backgroundService = new PreflightBackgroundService(
            preflightChannel.Reader,
            scopeFactoryMock.Object,
            new Mock<ILogger<PreflightBackgroundService>>().Object,
            lifecycle);
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
    public async Task StartRecordsWhoStartedTheJob()
    {
        var (jobId, uploadId, mandate, user) = await CreateAndStartJobAsync();

        // Through the real service, so a supplier lost on the way to the lifecycle shows here: the execution
        // protocol would no longer name who started a run.
        runRecorderMock.Verify(
            r => r.RecordJobStartedAsync(
                It.Is<ProcessingJob>(job => job.Id == jobId),
                mandate,
                Supplier.ForUser(user.Id),
                It.Is<UploadInfo>(upload => upload.Id == uploadId)),
            Times.Once);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task StartRecordsWhetherTheStarterMayDeliver(bool mayDeliver)
    {
        // Started by a signed in user, so a service that settled for a known supplier instead of asking the rule
        // would let the run be delivered here, and a staging that nobody may deliver would be back.
        var (jobId, _, _, _) = await CreateAndStartJobAsync(mayDeliver);

        var job = processingService.GetJob(jobId);
        Assert.IsNotNull(job);
        Assert.AreEqual(mayDeliver, job.MayDeliver);
    }

    [TestMethod]
    public async Task StartBindsTheDeliveryFieldsToTheJob()
    {
        var fields = new DeliveryFields(PartialDelivery: null, PrecursorDeliveryId: null, Comment: "created by geopilot");

        // Through the real service, so fields lost on the way to the lifecycle show here: the job would be no
        // attempt, geopilot would never create its delivery, and polling it would answer 404.
        var (jobId, _, _, _) = await CreateAndStartJobAsync(deliveryFields: fields);

        var job = processingService.GetJob(jobId);
        Assert.IsNotNull(job);
        Assert.AreEqual(fields, job.DeliveryIntent?.Fields);
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

    private async Task<(Guid JobId, Guid UploadId, Mandate Mandate, User User)> CreateAndStartJobAsync(bool mayDeliver = true, DeliveryFields? deliveryFields = null)
    {
        var pipelineId = "pipeline1";
        var mandate = new Mandate { Id = 1, Name = TestHelpers.Localized("Test Mandate"), PipelineId = pipelineId };
        var user = new User { FullName = "Test User", AuthIdentifier = "auth-123" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var uploadId = Guid.NewGuid();
        var uploadedFiles = ImmutableList.Create(new UploadedFileInfo("test.xtf", "uploads/test.xtf", 1024));
        await uploadStore.CreateUploadAsync(uploadId, uploadedFiles);

        mandateServiceMock.Setup(x => x.GetMandateForSupplierAsync(mandate.Id, Supplier.ForUser(user.Id))).ReturnsAsync(mandate);
        mandateServiceMock.Setup(x => x.MayDeliverAsync(mandate.Id, Supplier.ForUser(user.Id))).ReturnsAsync(mayDeliver);

        // The start instantiates the pipeline up front (without files) and attaches it to the job. On a preflight
        // failure the job lifecycle disposes the attached pipeline, so allow Dispose on the strict mock.
        var pipeline = new Mock<IPipeline>(MockBehavior.Strict);
        pipeline.SetupGet(p => p.Id).Returns(pipelineId);
        pipeline.Setup(p => p.Dispose());
        pipelineFactoryMock.Setup(x => x.CreatePipeline(pipelineId, It.IsAny<Guid>())).Returns(pipeline.Object);

        var job = await processingService.StartJobAsync(uploadId, mandate.Id, Supplier.ForUser(user.Id), deliveryFields);

        return (job.Id, uploadId, mandate, user);
    }

    private void SetupSuccessfulPreflight(Guid jobId, Guid uploadId, Mandate mandate, User user)
    {
        orchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(uploadId)).ReturnsAsync(new ScanResult(true));
        orchestrationServiceMock.Setup(x => x.RegisterJobFilesAsync(uploadId, jobId))
            .ReturnsAsync(() => new List<IPipelineFile> { new Mock<IPipelineFile>().Object });
    }
}
