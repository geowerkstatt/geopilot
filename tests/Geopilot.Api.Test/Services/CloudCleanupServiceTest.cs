using Geopilot.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class CloudCleanupServiceTest
{
    private Mock<ICloudStorageService> cloudStorageServiceMock;
    private Mock<ILogger<CloudCleanupService>> loggerMock;
    private CloudStorageOptions cloudStorageOptions;
    private CloudCleanupService service;

    [TestInitialize]
    public void Initialize()
    {
        cloudStorageServiceMock = new Mock<ICloudStorageService>(MockBehavior.Strict);
        loggerMock = new Mock<ILogger<CloudCleanupService>>();

        cloudStorageOptions = new CloudStorageOptions { CleanupAgeHours = 48 };

        var optionsMock = new Mock<IOptions<CloudStorageOptions>>();
        optionsMock.Setup(o => o.Value).Returns(cloudStorageOptions);

        service = new CloudCleanupService(
            cloudStorageServiceMock.Object,
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

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync($"uploads/{staleJobId}/"), Times.Once);
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

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync($"uploads/{staleJobId}/"), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupAsyncHandlesEmptyBucket()
    {
        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>());

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupAsyncSkipsNonGuidPrefixes()
    {
        var staleTimestamp = DateTime.UtcNow.AddHours(-49);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ("uploads/not-a-guid/file.xtf", 1024, staleTimestamp),
            });

        await service.RunCleanupAsync();

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

        await service.RunCleanupAsync();

        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync($"uploads/{staleJobId}/"), Times.Once);
        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync($"uploads/{recentJobId}/"), Times.Never);
    }
}
