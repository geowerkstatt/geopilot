using Geopilot.Api.FileAccess;
using Geopilot.Api.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Immutable;
using System.Globalization;

namespace Geopilot.Api.Test.Validation;

[TestClass]
public class ValidationJobCleanupServiceTest
{
    private const double RetentionHours = 24;
    private Mock<IValidationJobStore> jobStoreMock;
    private Mock<IDirectoryProvider> directoryProviderMock;
    private Mock<ILogger<ValidationJobCleanupService>> loggerMock;
    private string tempUploadRoot;
    private ValidationJobCleanupService service;

    [TestInitialize]
    public void Setup()
    {
        jobStoreMock = new Mock<IValidationJobStore>();
        directoryProviderMock = new Mock<IDirectoryProvider>();
        loggerMock = new Mock<ILogger<ValidationJobCleanupService>>();

        var configDict = new Dictionary<string, string?>
        {
            { "Validation:JobRetentionHours", RetentionHours.ToString(CultureInfo.InvariantCulture) },
            { "Validation:CleanupIntervalHours", "24" },
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        service = new ValidationJobCleanupService(
            jobStoreMock.Object,
            directoryProviderMock.Object,
            loggerMock.Object,
            configuration);

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
        jobStoreMock.Setup(s => s.GetJob(orphanJobId)).Returns((ValidationJob?)null);
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

        var oldJob = new ValidationJob(
            expiredJobId,
            null,
            null,
            null,
            ImmutableDictionary<string, ValidatorResult?>.Empty,
            Status.Created,
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
