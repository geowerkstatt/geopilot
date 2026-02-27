using Geopilot.Api.Services;
using Geopilot.Api.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class CloudCleanupServiceTest
{
    private Mock<ICloudStorageService> cloudStorageServiceMock;
    private Mock<IValidationJobStore> jobStoreMock;
    private Mock<ILogger<CloudCleanupService>> loggerMock;
    private CloudStorageOptions cloudStorageOptions;
    private CloudCleanupService service;

    [TestInitialize]
    public void Initialize()
    {
        cloudStorageServiceMock = new Mock<ICloudStorageService>(MockBehavior.Strict);
        jobStoreMock = new Mock<IValidationJobStore>(MockBehavior.Loose);
        loggerMock = new Mock<ILogger<CloudCleanupService>>();

        cloudStorageOptions = new CloudStorageOptions { CleanupAgeHours = 48 };

        var optionsMock = new Mock<IOptions<CloudStorageOptions>>();
        optionsMock.Setup(o => o.Value).Returns(cloudStorageOptions);

        service = new CloudCleanupService(
            cloudStorageServiceMock.Object,
            jobStoreMock.Object,
            loggerMock.Object,
            optionsMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        cloudStorageServiceMock.VerifyAll();
        service.Dispose();
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesStaleFiles()
    {
        var staleJobId = Guid.NewGuid();
        var staleTimestamp = DateTime.UtcNow.AddHours(-49);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{staleJobId}/test.xtf", 1024, staleTimestamp),
            });

        cloudStorageServiceMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{staleJobId}/"))
            .Returns(Task.CompletedTask);

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync($"uploads/{staleJobId}/"), Times.Once);
        jobStoreMock.Verify(s => s.RemoveJob(staleJobId), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupAsyncSkipsRecentFiles()
    {
        var recentJobId = Guid.NewGuid();
        var recentTimestamp = DateTime.UtcNow.AddHours(-1);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{recentJobId}/test.xtf", 1024, recentTimestamp),
            });

        jobStoreMock.Setup(s => s.GetJob(recentJobId)).Returns(new ValidationJob(recentJobId, null, null, null, System.Collections.Immutable.ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created, DateTime.Now));

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesMultipleStaleJobs()
    {
        var staleJobId1 = Guid.NewGuid();
        var staleJobId2 = Guid.NewGuid();
        var staleTimestamp = DateTime.UtcNow.AddHours(-49);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{staleJobId1}/file1.xtf", 1024, staleTimestamp),
                ($"uploads/{staleJobId2}/file2.xtf", 2048, staleTimestamp),
            });

        cloudStorageServiceMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{staleJobId1}/"))
            .Returns(Task.CompletedTask);
        cloudStorageServiceMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{staleJobId2}/"))
            .Returns(Task.CompletedTask);

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync(It.IsAny<string>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesOncePerJobWithMultipleFiles()
    {
        var staleJobId = Guid.NewGuid();
        var staleTimestamp = DateTime.UtcNow.AddHours(-49);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{staleJobId}/file1.xtf", 1024, staleTimestamp),
                ($"uploads/{staleJobId}/file2.xtf", 2048, staleTimestamp),
            });

        cloudStorageServiceMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{staleJobId}/"))
            .Returns(Task.CompletedTask);

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync($"uploads/{staleJobId}/"), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupAsyncHandlesEmptyBucket()
    {
        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>());

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesInvalidPrefixBlobs()
    {
        var staleTimestamp = DateTime.UtcNow.AddHours(-49);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ("uploads/not-a-guid/file.xtf", 1024, staleTimestamp),
            });

        cloudStorageServiceMock
            .Setup(s => s.DeleteAsync("uploads/not-a-guid/file.xtf"))
            .Returns(Task.CompletedTask);

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeleteAsync("uploads/not-a-guid/file.xtf"), Times.Once);
        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupAsyncOnlyDeletesStaleNotRecent()
    {
        var staleJobId = Guid.NewGuid();
        var recentJobId = Guid.NewGuid();

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{staleJobId}/test.xtf", 1024, DateTime.UtcNow.AddHours(-49)),
                ($"uploads/{recentJobId}/test.xtf", 1024, DateTime.UtcNow.AddHours(-1)),
            });

        cloudStorageServiceMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{staleJobId}/"))
            .Returns(Task.CompletedTask);

        jobStoreMock.Setup(s => s.GetJob(recentJobId)).Returns(new ValidationJob(recentJobId, null, null, null, System.Collections.Immutable.ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created, DateTime.Now));

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync($"uploads/{staleJobId}/"), Times.Once);
        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync($"uploads/{recentJobId}/"), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesOversizedFiles()
    {
        var jobId = Guid.NewGuid();
        var oversizedBytes = (long)cloudStorageOptions.MaxFileSizeMB * 1024 * 1024 + 1;

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{jobId}/test.xtf", oversizedBytes, DateTime.UtcNow),
            });

        cloudStorageServiceMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{jobId}/"))
            .Returns(Task.CompletedTask);

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync($"uploads/{jobId}/"), Times.Once);
        jobStoreMock.Verify(s => s.RemoveJob(jobId), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupAsyncSkipsNormalSizedFiles()
    {
        var jobId = Guid.NewGuid();
        var normalBytes = (long)cloudStorageOptions.MaxFileSizeMB * 1024 * 1024;

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{jobId}/test.xtf", normalBytes, DateTime.UtcNow),
            });

        jobStoreMock.Setup(s => s.GetJob(jobId)).Returns(new ValidationJob(jobId, null, null, null, System.Collections.Immutable.ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Created, DateTime.Now));

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesOrphanedJobBlobs()
    {
        var orphanJobId = Guid.NewGuid();

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{orphanJobId}/test.xtf", 1024, DateTime.UtcNow),
            });

        jobStoreMock.Setup(s => s.GetJob(orphanJobId)).Returns((ValidationJob?)null);

        cloudStorageServiceMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{orphanJobId}/"))
            .Returns(Task.CompletedTask);

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync($"uploads/{orphanJobId}/"), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesBlobsOutsideUploadsPrefix()
    {
        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>());

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync(string.Empty))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ("rogue-blob.txt", 512, DateTime.UtcNow),
                ("other/stuff.bin", 1024, DateTime.UtcNow),
                ("uploads/some-valid-path", 256, DateTime.UtcNow),
            });

        cloudStorageServiceMock
            .Setup(s => s.DeleteAsync("rogue-blob.txt"))
            .Returns(Task.CompletedTask);
        cloudStorageServiceMock
            .Setup(s => s.DeleteAsync("other/stuff.bin"))
            .Returns(Task.CompletedTask);

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeleteAsync("rogue-blob.txt"), Times.Once);
        cloudStorageServiceMock.Verify(s => s.DeleteAsync("other/stuff.bin"), Times.Once);
        cloudStorageServiceMock.Verify(s => s.DeleteAsync("uploads/some-valid-path"), Times.Never);
    }

    private void SetupEmptyContainerListing()
    {
        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync(string.Empty))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>());
    }
}
