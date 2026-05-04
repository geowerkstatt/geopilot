using Geopilot.Api.FileAccess;
using Geopilot.Api.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Test.Validation;

[TestClass]
public class ProcessingJobCleanupServiceTest
{
    private const double RetentionHours = 24;
    private Mock<IProcessingJobStore> jobStoreMock;
    private Mock<IDirectoryProvider> directoryProviderMock;
    private Mock<ILogger<ProcessingJobCleanupService>> loggerMock;
    private string tempUploadRoot;
    private ProcessingJobCleanupService service;

    [TestInitialize]
    public void Setup()
    {
        jobStoreMock = new Mock<IProcessingJobStore>();
        directoryProviderMock = new Mock<IDirectoryProvider>();
        loggerMock = new Mock<ILogger<ProcessingJobCleanupService>>();

        var ProcessingOptions = new ProcessingOptions
        {
            JobRetention = TimeSpan.FromHours(RetentionHours),
            JobCleanupInterval = TimeSpan.FromHours(24),
            JobTimeout = TimeSpan.FromHours(12),
        };

        var optionsMock = new Mock<IOptions<ProcessingOptions>>();
        optionsMock.Setup(o => o.Value).Returns(ProcessingOptions);

        service = new ProcessingJobCleanupService(
            jobStoreMock.Object,
            directoryProviderMock.Object,
            loggerMock.Object,
            optionsMock.Object);

        tempUploadRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempUploadRoot);
        directoryProviderMock.Setup(d => d.UploadDirectory).Returns(tempUploadRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        jobStoreMock.VerifyAll();
        directoryProviderMock.VerifyAll();

        if (Directory.Exists(tempUploadRoot))
            Directory.Delete(tempUploadRoot, true);

        service.Dispose();
    }

    [TestMethod]
    public void RunCleanupDeletesOrphanedFolders()
    {
        var orphanJobId = Guid.NewGuid();
        var orphanDir = Path.Combine(tempUploadRoot, orphanJobId.ToString());
        Directory.CreateDirectory(orphanDir);

        directoryProviderMock.Setup(d => d.GetUploadDirectoryPath(orphanJobId)).Returns(orphanDir);
        jobStoreMock.Setup(s => s.GetJob(orphanJobId)).Returns((ProcessingJob?)null);
        jobStoreMock.Setup(s => s.RemoveJob(orphanJobId)).Returns(true);

        service.RunCleanup();

        Assert.IsFalse(Directory.Exists(orphanDir));
        jobStoreMock.Verify(s => s.RemoveJob(orphanJobId), Times.Once);
    }

    [TestMethod]
    public void RunCleanupDeletesExpiredJobs()
    {
        var expiredJobId = Guid.NewGuid();
        var expiredDir = Path.Combine(tempUploadRoot, expiredJobId.ToString());
        Directory.CreateDirectory(expiredDir);

        var oldJob = new ProcessingJob(
            expiredJobId,
            new List<ProcessingJobFile>(),
            null,
            DateTime.UtcNow.AddHours(-RetentionHours - 1)); // older than retention

        directoryProviderMock.Setup(d => d.GetUploadDirectoryPath(expiredJobId)).Returns(expiredDir);
        jobStoreMock.Setup(s => s.GetJob(expiredJobId)).Returns(oldJob);
        jobStoreMock.Setup(s => s.RemoveJob(expiredJobId)).Returns(true);

        service.RunCleanup();

        Assert.IsFalse(Directory.Exists(expiredDir));
        jobStoreMock.Verify(s => s.RemoveJob(expiredJobId), Times.Once);
    }

    [TestMethod]
    public void RunCleanupSkipsNonGuidFolders()
    {
        var nonGuidDir = Path.Combine(tempUploadRoot, "not-a-guid");
        Directory.CreateDirectory(nonGuidDir);

        service.RunCleanup();

        Assert.IsTrue(Directory.Exists(nonGuidDir));
        jobStoreMock.Verify(s => s.RemoveJob(It.IsAny<Guid>()), Times.Never);
    }
}
