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

namespace Geopilot.Api.Test.Validation;

[TestClass]
public class CloudUploadPollingTest
{
    private ValidationJobStore jobStore;
    private Channel<PreflightRequest> preflightChannel;
    private ValidationService validationService;
    private PreflightBackgroundService backgroundService;

    private Mock<ICloudOrchestrationService> cloudOrchestrationServiceMock;
    private Mock<ICloudStorageService> cloudStorageServiceMock;
    private Mock<IMandateService> mandateServiceMock;
    private Mock<IFileProvider> fileProviderMock;
    private Mock<IPipelineFactory> pipelineFactoryMock;
    private Context context;

    [TestInitialize]
    public void Initialize()
    {
        cloudOrchestrationServiceMock = new Mock<ICloudOrchestrationService>(MockBehavior.Strict);
        cloudStorageServiceMock = new Mock<ICloudStorageService>(MockBehavior.Strict);
        mandateServiceMock = new Mock<IMandateService>(MockBehavior.Strict);
        fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);
        pipelineFactoryMock = new Mock<IPipelineFactory>(MockBehavior.Strict);
        context = AssemblyInitialize.DbFixture.GetTestContext();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        jobStore = new ValidationJobStore(scopeFactoryMock.Object);
        preflightChannel = Channel.CreateUnbounded<PreflightRequest>();

        validationService = new ValidationService(
            jobStore,
            mandateServiceMock.Object,
            fileProviderMock.Object,
            pipelineFactoryMock.Object,
            cloudOrchestrationServiceMock.Object,
            preflightChannel.Writer);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IValidationJobStore))).Returns(jobStore);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICloudOrchestrationService))).Returns(cloudOrchestrationServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICloudStorageService))).Returns(cloudStorageServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IMandateService))).Returns(mandateServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IFileProvider))).Returns(fileProviderMock.Object);
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
        var (jobId, _, _) = await CreateAndStartCloudJobAsync();

        var polledJob = validationService.GetJob(jobId);

        Assert.IsNotNull(polledJob);
        Assert.AreEqual(Status.VerifyingUpload, polledJob.Status);
    }

    [TestMethod]
    public async Task PollReturnsProcessingAfterSuccessfulPreflight()
    {
        var (jobId, mandate, user) = await CreateAndStartCloudJobAsync();

        SetupSuccessfulPreflight(jobId, mandate, user);

        var request = await preflightChannel.Reader.ReadAsync();
        await backgroundService.ProcessRequestAsync(request);

        var polledJob = validationService.GetJob(jobId);

        Assert.IsNotNull(polledJob);
        Assert.AreEqual(Status.Processing, polledJob.Status);
        Assert.AreEqual(mandate.Id, polledJob.MandateId);
    }

    [TestMethod]
    [DataRow(PreflightFailureReason.IncompleteUpload)]
    [DataRow(PreflightFailureReason.ThreatDetected)]
    [DataRow(PreflightFailureReason.SizeExceeded)]
    public async Task PollReturnsFailedAfterPreflightFailure(PreflightFailureReason reason)
    {
        var (jobId, _, _) = await CreateAndStartCloudJobAsync();

        cloudOrchestrationServiceMock
            .Setup(x => x.RunPreflightChecksAsync(jobId))
            .ThrowsAsync(new CloudUploadPreflightException(reason, "Preflight check failed."));
        cloudStorageServiceMock
            .Setup(x => x.DeletePrefixAsync($"uploads/{jobId}/"))
            .Returns(Task.CompletedTask);

        var request = await preflightChannel.Reader.ReadAsync();
        await backgroundService.ProcessRequestAsync(request);

        var polledJob = validationService.GetJob(jobId);

        Assert.IsNotNull(polledJob);
        Assert.AreEqual(Status.Failed, polledJob.Status);
    }

    [TestMethod]
    public async Task PollReturnsFailedAfterGenericException()
    {
        var (jobId, _, _) = await CreateAndStartCloudJobAsync();

        cloudOrchestrationServiceMock
            .Setup(x => x.RunPreflightChecksAsync(jobId))
            .ThrowsAsync(new InvalidOperationException("Unexpected error during processing."));
        cloudStorageServiceMock
            .Setup(x => x.DeletePrefixAsync($"uploads/{jobId}/"))
            .Returns(Task.CompletedTask);

        var request = await preflightChannel.Reader.ReadAsync();
        await backgroundService.ProcessRequestAsync(request);

        var polledJob = validationService.GetJob(jobId);

        Assert.IsNotNull(polledJob);
        Assert.AreEqual(Status.Failed, polledJob.Status);
    }

    [TestMethod]
    public async Task PollReturnsFailedEvenWhenCleanupFails()
    {
        var (jobId, _, _) = await CreateAndStartCloudJobAsync();

        cloudOrchestrationServiceMock
            .Setup(x => x.RunPreflightChecksAsync(jobId))
            .ThrowsAsync(new CloudUploadPreflightException(PreflightFailureReason.ThreatDetected, "Threat detected."));
        cloudStorageServiceMock
            .Setup(x => x.DeletePrefixAsync($"uploads/{jobId}/"))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable."));

        var request = await preflightChannel.Reader.ReadAsync();
        await backgroundService.ProcessRequestAsync(request);

        var polledJob = validationService.GetJob(jobId);

        Assert.IsNotNull(polledJob);
        Assert.AreEqual(Status.Failed, polledJob.Status);
    }

    private async Task<(Guid JobId, Mandate Mandate, User User)> CreateAndStartCloudJobAsync()
    {
        var pipelineId = "pipeline1";
        var mandate = new Mandate { Id = 1, Name = "Test Mandate", PipelineId = pipelineId };
        var user = new User { FullName = "Test User", AuthIdentifier = "auth-123" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var cloudFiles = ImmutableList.Create(new CloudFileInfo("test.xtf", "uploads/test.xtf", 1024));

        var job = jobStore.CreateJob();
        jobStore.AddUploadInfoToJob(job.Id, UploadMethod.Cloud, cloudFiles);

        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, user)).ReturnsAsync(mandate);

        await validationService.StartJobAsync(job.Id, mandate.Id, user);

        return (job.Id, mandate, user);
    }

    private void SetupSuccessfulPreflight(Guid jobId, Mandate mandate, User user)
    {
        var pipeline = new Mock<IPipeline>(MockBehavior.Strict);

        cloudOrchestrationServiceMock.Setup(x => x.RunPreflightChecksAsync(jobId)).Returns(Task.CompletedTask);
        cloudOrchestrationServiceMock.Setup(x => x.StageFilesLocallyAsync(jobId))
            .Returns(() =>
            {
                // The real StageFilesLocallyAsync calls AddFileToJob, which transitions the store to Ready.
                var stagedJob = jobStore.AddFileToJob(jobId, "test.xtf", "random.xtf");
                return Task.FromResult(stagedJob);
            });
        mandateServiceMock.Setup(x => x.GetMandateForUser(mandate.Id, It.Is<User>(u => u.AuthIdentifier == user.AuthIdentifier))).ReturnsAsync(mandate);
        fileProviderMock.Setup(x => x.Initialize(jobId));
        fileProviderMock.Setup(x => x.GetFilePath("random.xtf")).Returns("path/to/random.xtf");
        pipelineFactoryMock.Setup(x => x.CreatePipeline(mandate.PipelineId!, It.Is<ICollection<IPipelineFile>>(f => f.Any(file => file.OriginalFileName == "test.xtf")), It.IsAny<Guid>())).Returns(pipeline.Object);
    }
}
