using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Api.Test.Processing;

[TestClass]
public class ProcessingJobCleanupServiceTest
{
    private const double JobRetentionHours = 24;
    private const double DownloadRetentionHours = 1;
    private const double VisualizationRetentionHours = 0.5;
    private Mock<IProcessingJobStore> jobStoreMock;
    private Mock<IDirectoryProvider> directoryProviderMock;
    private Mock<ICloudOrchestrationService> cloudOrchestrationServiceMock;
    private Mock<ILogger<ProcessingJobCleanupService>> loggerMock;
    private Context context;
    private string tempAssetRoot;
    private string tempDownloadRoot;
    private string tempVisualizationRoot;
    private string tempPipelineRoot;
    private ProcessingJobCleanupService service;

    [TestInitialize]
    public void Setup()
    {
        jobStoreMock = new Mock<IProcessingJobStore>();
        jobStoreMock.Setup(s => s.GetJobIds()).Returns(Array.Empty<Guid>());
        directoryProviderMock = new Mock<IDirectoryProvider>();
        loggerMock = new Mock<ILogger<ProcessingJobCleanupService>>();
        context = AssemblyInitialize.DbFixture.GetTestContext();

        var processingOptions = new ProcessingOptions
        {
            JobRetention = TimeSpan.FromHours(JobRetentionHours),
            DownloadRetention = TimeSpan.FromHours(DownloadRetentionHours),
            VisualizationRetention = TimeSpan.FromHours(VisualizationRetentionHours),
            JobCleanupInterval = TimeSpan.FromHours(24),
            JobTimeout = TimeSpan.FromHours(12),
        };

        var optionsMock = new Mock<IOptions<ProcessingOptions>>();
        optionsMock.Setup(o => o.Value).Returns(processingOptions);

        cloudOrchestrationServiceMock = new Mock<ICloudOrchestrationService>();
        cloudOrchestrationServiceMock.Setup(c => c.ReleaseUploadAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(Context))).Returns(context);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(ICloudOrchestrationService))).Returns(cloudOrchestrationServiceMock.Object);
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        service = new ProcessingJobCleanupService(
            jobStoreMock.Object,
            directoryProviderMock.Object,
            scopeFactoryMock.Object,
            loggerMock.Object,
            optionsMock.Object);

        tempAssetRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        tempDownloadRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        tempVisualizationRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        tempPipelineRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempAssetRoot);
        Directory.CreateDirectory(tempDownloadRoot);
        Directory.CreateDirectory(tempVisualizationRoot);
        Directory.CreateDirectory(tempPipelineRoot);
        directoryProviderMock.Setup(d => d.AssetDirectory).Returns(tempAssetRoot);
        directoryProviderMock.Setup(d => d.DownloadDirectory).Returns(tempDownloadRoot);
        directoryProviderMock.Setup(d => d.VisualizationDirectory).Returns(tempVisualizationRoot);
        directoryProviderMock.Setup(d => d.PipelineDirectory).Returns(tempPipelineRoot);
        directoryProviderMock
            .Setup(d => d.GetAssetDirectoryPath(It.IsAny<Guid>()))
            .Returns<Guid>(jobId => Path.Combine(tempAssetRoot, jobId.ToString()));
        directoryProviderMock
            .Setup(d => d.GetDownloadDirectoryPath(It.IsAny<Guid>()))
            .Returns<Guid>(jobId => Path.Combine(tempDownloadRoot, jobId.ToString()));
        directoryProviderMock
            .Setup(d => d.GetVisualizationDirectoryPath(It.IsAny<Guid>()))
            .Returns<Guid>(jobId => Path.Combine(tempVisualizationRoot, jobId.ToString()));
        directoryProviderMock
            .Setup(d => d.GetPipelineDirectoryPath(It.IsAny<Guid>()))
            .Returns<Guid>(jobId => Path.Combine(tempPipelineRoot, jobId.ToString()));
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var root in new[] { tempAssetRoot, tempDownloadRoot, tempVisualizationRoot, tempPipelineRoot })
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        context.Dispose();
        service.Dispose();
    }

    [TestMethod]
    public async Task RunCleanupRetiresOrphanedJob()
    {
        var orphanJobId = Guid.NewGuid();
        var (assetDir, downloadDir, visualizationDir, pipelineDir) = CreateJobDirectories(orphanJobId);

        jobStoreMock.Setup(s => s.GetJob(orphanJobId)).Returns((ProcessingJob?)null);
        jobStoreMock.Setup(s => s.RemoveJob(orphanJobId)).Returns(true);

        await service.RunCleanupAsync();
        Assert.IsFalse(Directory.Exists(downloadDir));
        Assert.IsFalse(Directory.Exists(visualizationDir));
        Assert.IsFalse(Directory.Exists(assetDir));
        Assert.IsFalse(Directory.Exists(pipelineDir));
        jobStoreMock.Verify(s => s.RemoveJob(orphanJobId), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupRetiresExpiredUnsubmittedJob()
    {
        var jobId = Guid.NewGuid();
        var (assetDir, downloadDir, visualizationDir, pipelineDir) = CreateJobDirectories(jobId);

        var oldJob = new ProcessingJob(
            jobId,
            Guid.NewGuid(),
            new List<ProcessingJobFile>(),
            null,
            DateTime.UtcNow.AddHours(-JobRetentionHours - 1))
        {
            State = ProcessingState.Success,
        };

        jobStoreMock.Setup(s => s.GetJob(jobId)).Returns(oldJob);
        jobStoreMock.Setup(s => s.RemoveJob(jobId)).Returns(true);

        await service.RunCleanupAsync();
        Assert.IsFalse(Directory.Exists(downloadDir));
        Assert.IsFalse(Directory.Exists(visualizationDir));
        Assert.IsFalse(Directory.Exists(assetDir));
        Assert.IsFalse(Directory.Exists(pipelineDir));
        jobStoreMock.Verify(s => s.RemoveJob(jobId), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupKeepsAssetDirForSubmittedDelivery()
    {
        var jobId = Guid.NewGuid();
        var (assetDir, downloadDir, visualizationDir, pipelineDir) = CreateJobDirectories(jobId);

        // The job has been submitted as a delivery; the asset directory must survive cleanup.
        SeedDelivery(jobId);

        var oldJob = new ProcessingJob(
            jobId,
            Guid.NewGuid(),
            new List<ProcessingJobFile>(),
            null,
            DateTime.UtcNow.AddHours(-JobRetentionHours - 1))
        {
            State = ProcessingState.Success,
        };

        jobStoreMock.Setup(s => s.GetJob(jobId)).Returns(oldJob);
        jobStoreMock.Setup(s => s.RemoveJob(jobId)).Returns(true);

        await service.RunCleanupAsync();
        Assert.IsFalse(Directory.Exists(downloadDir), "Download directory should be cleaned on JobRetention.");
        Assert.IsFalse(Directory.Exists(visualizationDir), "Visualization directory should be cleaned on JobRetention.");
        Assert.IsFalse(Directory.Exists(pipelineDir), "Pipeline working directory should be cleaned on JobRetention.");
        Assert.IsTrue(Directory.Exists(assetDir), "Asset directory must survive cleanup for submitted deliveries.");
        jobStoreMock.Verify(s => s.RemoveJob(jobId), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupExpiresDownloadsBeforeFullRetention()
    {
        var jobId = Guid.NewGuid();
        var (assetDir, downloadDir, visualizationDir, pipelineDir) = CreateJobDirectories(jobId);

        var partlyExpiredJob = new ProcessingJob(
            jobId,
            Guid.NewGuid(),
            new List<ProcessingJobFile>(),
            null,
            DateTime.UtcNow.AddHours(-(DownloadRetentionHours + 1)));

        jobStoreMock.Setup(s => s.GetJob(jobId)).Returns(partlyExpiredJob);

        await service.RunCleanupAsync();
        Assert.IsTrue(Directory.Exists(assetDir), "Asset directory should still exist within JobRetention.");
        Assert.IsTrue(Directory.Exists(pipelineDir), "Pipeline working directory should still exist within JobRetention.");
        Assert.IsFalse(Directory.Exists(downloadDir), "Download directory should be cleaned after DownloadRetention.");
        Assert.IsFalse(Directory.Exists(visualizationDir), "Visualization directory should be cleaned after VisualizationRetention.");
        jobStoreMock.Verify(s => s.RemoveJob(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupExpiresVisualizationsBeforeDownloadRetention()
    {
        var jobId = Guid.NewGuid();
        var (assetDir, downloadDir, visualizationDir, pipelineDir) = CreateJobDirectories(jobId);

        var partlyExpiredJob = new ProcessingJob(
            jobId,
            Guid.NewGuid(),
            new List<ProcessingJobFile>(),
            null,
            DateTime.UtcNow.AddHours(-(VisualizationRetentionHours + 0.25)));

        jobStoreMock.Setup(s => s.GetJob(jobId)).Returns(partlyExpiredJob);

        await service.RunCleanupAsync();
        Assert.IsTrue(Directory.Exists(assetDir), "Asset directory should still exist within JobRetention.");
        Assert.IsTrue(Directory.Exists(downloadDir), "Download directory should still exist within DownloadRetention.");
        Assert.IsTrue(Directory.Exists(pipelineDir), "Pipeline working directory should still exist within JobRetention.");
        Assert.IsFalse(Directory.Exists(visualizationDir), "Visualization directory should be cleaned after VisualizationRetention.");
        jobStoreMock.Verify(s => s.RemoveJob(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupReleasesTheUploadOfARetiredJob()
    {
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        CreateJobDirectories(jobId);

        var oldJob = new ProcessingJob(
            jobId,
            uploadId,
            new List<ProcessingJobFile>(),
            null,
            DateTime.UtcNow.AddHours(-JobRetentionHours - 1))
        {
            State = ProcessingState.Success,
        };

        jobStoreMock.Setup(s => s.GetJob(jobId)).Returns(oldJob);
        jobStoreMock.Setup(s => s.RemoveJob(jobId)).Returns(true);

        await service.RunCleanupAsync();

        cloudOrchestrationServiceMock.Verify(c => c.ReleaseUploadAsync(uploadId), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupRetiresAJobWithoutAnyDirectory()
    {
        // A job that produced no assets and whose pipeline directory was already disposed leaves no
        // directory behind, so the job store is the only place the cleanup can find it.
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var oldJob = new ProcessingJob(
            jobId,
            uploadId,
            new List<ProcessingJobFile>(),
            null,
            DateTime.UtcNow.AddHours(-JobRetentionHours - 1))
        {
            State = ProcessingState.Success,
        };

        jobStoreMock.Setup(s => s.GetJobIds()).Returns(new[] { jobId });
        jobStoreMock.Setup(s => s.GetJob(jobId)).Returns(oldJob);
        jobStoreMock.Setup(s => s.RemoveJob(jobId)).Returns(true);

        await service.RunCleanupAsync();

        jobStoreMock.Verify(s => s.RemoveJob(jobId), Times.Once);
        cloudOrchestrationServiceMock.Verify(c => c.ReleaseUploadAsync(uploadId), Times.Once);
    }

    [TestMethod]
    [DataRow(ProcessingState.Pending)]
    [DataRow(ProcessingState.Running)]
    public async Task RunCleanupLeavesAStuckJobAlone(ProcessingState state)
    {
        // Past its retention but not finished: preflight or the pipeline is hanging. Retiring it would
        // delete the uploaded blobs, the only remaining copy, from under a run that may yet continue.
        var jobId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var (assetDir, downloadDir, visualizationDir, pipelineDir) = CreateJobDirectories(jobId);

        var stuckJob = new ProcessingJob(
            jobId,
            uploadId,
            new List<ProcessingJobFile>(),
            null,
            DateTime.UtcNow.AddHours(-JobRetentionHours - 1))
        {
            State = state,
        };

        jobStoreMock.Setup(s => s.GetJobIds()).Returns(new[] { jobId });
        jobStoreMock.Setup(s => s.GetJob(jobId)).Returns(stuckJob);

        await service.RunCleanupAsync();

        cloudOrchestrationServiceMock.Verify(c => c.ReleaseUploadAsync(It.IsAny<Guid>()), Times.Never, "the upload is the only copy left and the run may still need it");
        jobStoreMock.Verify(s => s.RemoveJob(It.IsAny<Guid>()), Times.Never, "disposing the pipeline would pull its working directory away mid-run");
        Assert.IsTrue(Directory.Exists(assetDir));
        Assert.IsTrue(Directory.Exists(pipelineDir));

        // Downloads and visualizations still age out on their own, shorter retentions.
        Assert.IsFalse(Directory.Exists(downloadDir));
        Assert.IsFalse(Directory.Exists(visualizationDir));
    }

    [TestMethod]
    public async Task RunCleanupKeepsFreshJobDirectories()
    {
        var jobId = Guid.NewGuid();
        var (assetDir, downloadDir, visualizationDir, pipelineDir) = CreateJobDirectories(jobId);

        var freshJob = new ProcessingJob(
            jobId,
            Guid.NewGuid(),
            new List<ProcessingJobFile>(),
            null,
            DateTime.UtcNow);

        jobStoreMock.Setup(s => s.GetJob(jobId)).Returns(freshJob);

        await service.RunCleanupAsync();
        Assert.IsTrue(Directory.Exists(assetDir));
        Assert.IsTrue(Directory.Exists(downloadDir));
        Assert.IsTrue(Directory.Exists(visualizationDir));
        Assert.IsTrue(Directory.Exists(pipelineDir));
        jobStoreMock.Verify(s => s.RemoveJob(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupRetiresOrphanedPipelineDirectory()
    {
        // A pipeline working directory left behind by a hard restart: the in-memory job store
        // is empty and no upload or asset directory exists that would flag the job for retirement.
        var orphanJobId = Guid.NewGuid();
        var pipelineDir = Path.Combine(tempPipelineRoot, orphanJobId.ToString());
        Directory.CreateDirectory(pipelineDir);

        jobStoreMock.Setup(s => s.GetJob(orphanJobId)).Returns((ProcessingJob?)null);

        await service.RunCleanupAsync();

        Assert.IsFalse(Directory.Exists(pipelineDir));
        jobStoreMock.Verify(s => s.RemoveJob(orphanJobId), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupSkipsNonGuidFolders()
    {
        var nonGuidDir = Path.Combine(tempAssetRoot, "not-a-guid");
        var nonGuidPipelineDir = Path.Combine(tempPipelineRoot, "not-a-guid");
        Directory.CreateDirectory(nonGuidDir);
        Directory.CreateDirectory(nonGuidPipelineDir);

        await service.RunCleanupAsync();

        Assert.IsTrue(Directory.Exists(nonGuidDir));
        Assert.IsTrue(Directory.Exists(nonGuidPipelineDir));
        jobStoreMock.Verify(s => s.RemoveJob(It.IsAny<Guid>()), Times.Never);
    }

    private (string Asset, string Download, string Visualization, string Pipeline) CreateJobDirectories(Guid jobId)
    {
        var asset = Path.Combine(tempAssetRoot, jobId.ToString());
        var download = Path.Combine(tempDownloadRoot, jobId.ToString());
        var visualization = Path.Combine(tempVisualizationRoot, jobId.ToString());
        var pipeline = Path.Combine(tempPipelineRoot, jobId.ToString());
        Directory.CreateDirectory(asset);
        Directory.CreateDirectory(download);
        Directory.CreateDirectory(visualization);
        Directory.CreateDirectory(pipeline);
        return (asset, download, visualization, pipeline);
    }

    private void SeedDelivery(Guid jobId)
    {
        var mandate = context.Mandates.First();
        var user = context.Users.First();
        context.Deliveries.Add(new Delivery
        {
            JobId = jobId,
            Mandate = mandate,
            DeclaringUser = user,
            Comment = "test",
            Assets = new List<Asset>(),
        });
        context.SaveChanges();
    }
}
