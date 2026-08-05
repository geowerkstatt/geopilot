using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Api.Test.Validation;

[TestClass]
public class ProcessingJobCleanupServiceTest
{
    private const double JobRetentionHours = 24;
    private const double DownloadRetentionHours = 1;
    private Mock<IProcessingJobStore> jobStoreMock;
    private Mock<IDirectoryProvider> directoryProviderMock;
    private Mock<ILogger<ProcessingJobCleanupService>> loggerMock;
    private Context context;
    private string tempUploadRoot;
    private string tempAssetRoot;
    private string tempDownloadRoot;
    private string tempPipelineRoot;
    private ProcessingJobCleanupService service;

    [TestInitialize]
    public void Setup()
    {
        jobStoreMock = new Mock<IProcessingJobStore>();
        directoryProviderMock = new Mock<IDirectoryProvider>();
        loggerMock = new Mock<ILogger<ProcessingJobCleanupService>>();
        context = AssemblyInitialize.DbFixture.GetTestContext();

        var processingOptions = new ProcessingOptions
        {
            JobRetention = TimeSpan.FromHours(JobRetentionHours),
            DownloadRetention = TimeSpan.FromHours(DownloadRetentionHours),
            JobCleanupInterval = TimeSpan.FromHours(24),
            JobTimeout = TimeSpan.FromHours(12),
        };

        var optionsMock = new Mock<IOptions<ProcessingOptions>>();
        optionsMock.Setup(o => o.Value).Returns(processingOptions);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(Context))).Returns(context);
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

        tempUploadRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        tempAssetRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        tempDownloadRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        tempPipelineRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempUploadRoot);
        Directory.CreateDirectory(tempAssetRoot);
        Directory.CreateDirectory(tempDownloadRoot);
        Directory.CreateDirectory(tempPipelineRoot);
        directoryProviderMock.Setup(d => d.UploadDirectory).Returns(tempUploadRoot);
        directoryProviderMock.Setup(d => d.AssetDirectory).Returns(tempAssetRoot);
        directoryProviderMock.Setup(d => d.DownloadDirectory).Returns(tempDownloadRoot);
        directoryProviderMock.Setup(d => d.PipelineDirectory).Returns(tempPipelineRoot);
        directoryProviderMock
            .Setup(d => d.GetUploadDirectoryPath(It.IsAny<Guid>()))
            .Returns<Guid>(jobId => Path.Combine(tempUploadRoot, jobId.ToString()));
        directoryProviderMock
            .Setup(d => d.GetAssetDirectoryPath(It.IsAny<Guid>()))
            .Returns<Guid>(jobId => Path.Combine(tempAssetRoot, jobId.ToString()));
        directoryProviderMock
            .Setup(d => d.GetDownloadDirectoryPath(It.IsAny<Guid>()))
            .Returns<Guid>(jobId => Path.Combine(tempDownloadRoot, jobId.ToString()));
        directoryProviderMock
            .Setup(d => d.GetPipelineDirectoryPath(It.IsAny<Guid>()))
            .Returns<Guid>(jobId => Path.Combine(tempPipelineRoot, jobId.ToString()));
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var root in new[] { tempUploadRoot, tempAssetRoot, tempDownloadRoot, tempPipelineRoot })
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        context.Dispose();
        service.Dispose();
    }

    [TestMethod]
    public void RunCleanupRetiresOrphanedJob()
    {
        var orphanJobId = Guid.NewGuid();
        var (uploadDir, assetDir, downloadDir, pipelineDir) = CreateJobDirectories(orphanJobId);

        jobStoreMock.Setup(s => s.GetJob(orphanJobId)).Returns((ProcessingJob?)null);
        jobStoreMock.Setup(s => s.RemoveJob(orphanJobId)).Returns(true);

        service.RunCleanup();

        Assert.IsFalse(Directory.Exists(uploadDir));
        Assert.IsFalse(Directory.Exists(downloadDir));
        Assert.IsFalse(Directory.Exists(assetDir));
        Assert.IsFalse(Directory.Exists(pipelineDir));
        jobStoreMock.Verify(s => s.RemoveJob(orphanJobId), Times.Once);
    }

    [TestMethod]
    public void RunCleanupRetiresExpiredUnsubmittedJob()
    {
        var jobId = Guid.NewGuid();
        var (uploadDir, assetDir, downloadDir, pipelineDir) = CreateJobDirectories(jobId);

        var oldJob = new ProcessingJob(
            jobId,
            new List<ProcessingJobFile>(),
            null,
            DateTime.UtcNow.AddHours(-JobRetentionHours - 1));

        jobStoreMock.Setup(s => s.GetJob(jobId)).Returns(oldJob);
        jobStoreMock.Setup(s => s.RemoveJob(jobId)).Returns(true);

        service.RunCleanup();

        Assert.IsFalse(Directory.Exists(uploadDir));
        Assert.IsFalse(Directory.Exists(downloadDir));
        Assert.IsFalse(Directory.Exists(assetDir));
        Assert.IsFalse(Directory.Exists(pipelineDir));
        jobStoreMock.Verify(s => s.RemoveJob(jobId), Times.Once);
    }

    [TestMethod]
    public void RunCleanupKeepsAssetDirForSubmittedDelivery()
    {
        var jobId = Guid.NewGuid();
        var (uploadDir, assetDir, downloadDir, pipelineDir) = CreateJobDirectories(jobId);

        // The job has been submitted as a delivery; the asset directory must survive cleanup.
        SeedDelivery(jobId);

        var oldJob = new ProcessingJob(
            jobId,
            new List<ProcessingJobFile>(),
            null,
            DateTime.UtcNow.AddHours(-JobRetentionHours - 1));

        jobStoreMock.Setup(s => s.GetJob(jobId)).Returns(oldJob);
        jobStoreMock.Setup(s => s.RemoveJob(jobId)).Returns(true);

        service.RunCleanup();

        Assert.IsFalse(Directory.Exists(uploadDir), "Upload directory should be cleaned on JobRetention.");
        Assert.IsFalse(Directory.Exists(downloadDir), "Download directory should be cleaned on JobRetention.");
        Assert.IsFalse(Directory.Exists(pipelineDir), "Pipeline working directory should be cleaned on JobRetention.");
        Assert.IsTrue(Directory.Exists(assetDir), "Asset directory must survive cleanup for submitted deliveries.");
        jobStoreMock.Verify(s => s.RemoveJob(jobId), Times.Once);
    }

    [TestMethod]
    public void RunCleanupExpiresDownloadsBeforeFullRetention()
    {
        var jobId = Guid.NewGuid();
        var (uploadDir, assetDir, downloadDir, pipelineDir) = CreateJobDirectories(jobId);

        var partlyExpiredJob = new ProcessingJob(
            jobId,
            new List<ProcessingJobFile>(),
            null,
            DateTime.UtcNow.AddHours(-(DownloadRetentionHours + 1)));

        jobStoreMock.Setup(s => s.GetJob(jobId)).Returns(partlyExpiredJob);

        service.RunCleanup();

        Assert.IsTrue(Directory.Exists(uploadDir), "Upload directory should still exist within JobRetention.");
        Assert.IsTrue(Directory.Exists(assetDir), "Asset directory should still exist within JobRetention.");
        Assert.IsTrue(Directory.Exists(pipelineDir), "Pipeline working directory should still exist within JobRetention.");
        Assert.IsFalse(Directory.Exists(downloadDir), "Download directory should be cleaned after DownloadRetention.");
        jobStoreMock.Verify(s => s.RemoveJob(It.IsAny<Guid>()), Times.Never);
    }

    [TestMethod]
    public void RunCleanupRetiresOrphanedPipelineDirectory()
    {
        // A pipeline working directory left behind by a hard restart: the in-memory job store
        // is empty and no upload or asset directory exists that would flag the job for retirement.
        var orphanJobId = Guid.NewGuid();
        var pipelineDir = Path.Combine(tempPipelineRoot, orphanJobId.ToString());
        Directory.CreateDirectory(pipelineDir);

        jobStoreMock.Setup(s => s.GetJob(orphanJobId)).Returns((ProcessingJob?)null);

        service.RunCleanup();

        Assert.IsFalse(Directory.Exists(pipelineDir));
        jobStoreMock.Verify(s => s.RemoveJob(orphanJobId), Times.Once);
    }

    [TestMethod]
    public void RunCleanupSkipsNonGuidFolders()
    {
        var nonGuidDir = Path.Combine(tempUploadRoot, "not-a-guid");
        var nonGuidPipelineDir = Path.Combine(tempPipelineRoot, "not-a-guid");
        Directory.CreateDirectory(nonGuidDir);
        Directory.CreateDirectory(nonGuidPipelineDir);

        service.RunCleanup();

        Assert.IsTrue(Directory.Exists(nonGuidDir));
        Assert.IsTrue(Directory.Exists(nonGuidPipelineDir));
        jobStoreMock.Verify(s => s.RemoveJob(It.IsAny<Guid>()), Times.Never);
    }

    private (string Upload, string Asset, string Download, string Pipeline) CreateJobDirectories(Guid jobId)
    {
        var upload = Path.Combine(tempUploadRoot, jobId.ToString());
        var asset = Path.Combine(tempAssetRoot, jobId.ToString());
        var download = Path.Combine(tempDownloadRoot, jobId.ToString());
        var pipeline = Path.Combine(tempPipelineRoot, jobId.ToString());
        Directory.CreateDirectory(upload);
        Directory.CreateDirectory(asset);
        Directory.CreateDirectory(download);
        Directory.CreateDirectory(pipeline);
        return (upload, asset, download, pipeline);
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
