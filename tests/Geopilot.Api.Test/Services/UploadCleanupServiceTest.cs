using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class UploadCleanupServiceTest
{
    private Mock<IUploadStorage> uploadStorageMock;
    private Mock<IUploadStore> uploadStoreMock;
    private Mock<ILogger<UploadCleanupService>> loggerMock;
    private CloudStorageOptions cloudStorageOptions;
    private UploadCleanupService service;

    [TestInitialize]
    public void Initialize()
    {
        uploadStorageMock = new Mock<IUploadStorage>(MockBehavior.Strict);
        uploadStoreMock = new Mock<IUploadStore>(MockBehavior.Loose);
        loggerMock = new Mock<ILogger<UploadCleanupService>>();

        cloudStorageOptions = new CloudStorageOptions { CleanupAgeHours = 48, MaxFileSizeMB = 2048 };

        var optionsMock = new Mock<IOptions<CloudStorageOptions>>();
        optionsMock.Setup(o => o.Value).Returns(cloudStorageOptions);

        service = new UploadCleanupService(
            uploadStorageMock.Object,
            uploadStoreMock.Object,
            loggerMock.Object,
            optionsMock.Object,
            Options.Create(new ProcessingOptions { JobRetention = TimeSpan.FromHours(24), JobTimeout = TimeSpan.FromMinutes(30) }));
    }

    [TestCleanup]
    public void Cleanup()
    {
        uploadStorageMock.VerifyAll();
        service.Dispose();
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesStaleFiles()
    {
        var staleUploadId = Guid.NewGuid();
        var staleTimestamp = DateTime.UtcNow.AddHours(-49);

        uploadStorageMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{staleUploadId}/test.xtf", 1024, staleTimestamp),
            });

        uploadStorageMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{staleUploadId}/"))
            .Returns(Task.CompletedTask);

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        uploadStorageMock.Verify(s => s.DeletePrefixAsync($"uploads/{staleUploadId}/"), Times.Once);
        uploadStoreMock.Verify(s => s.RemoveUpload(staleUploadId), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupAsyncSkipsRecentFiles()
    {
        var recentUploadId = Guid.NewGuid();
        var recentTimestamp = DateTime.UtcNow.AddHours(-1);

        uploadStorageMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{recentUploadId}/test.xtf", 1024, recentTimestamp),
            });

        uploadStoreMock.Setup(s => s.GetUpload(recentUploadId)).Returns(CreateUpload(recentUploadId));

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        uploadStorageMock.Verify(s => s.DeletePrefixAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesMultipleStaleUploads()
    {
        var staleUploadId1 = Guid.NewGuid();
        var staleUploadId2 = Guid.NewGuid();
        var staleTimestamp = DateTime.UtcNow.AddHours(-49);

        uploadStorageMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{staleUploadId1}/file1.xtf", 1024, staleTimestamp),
                ($"uploads/{staleUploadId2}/file2.xtf", 2048, staleTimestamp),
            });

        uploadStorageMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{staleUploadId1}/"))
            .Returns(Task.CompletedTask);
        uploadStorageMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{staleUploadId2}/"))
            .Returns(Task.CompletedTask);

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        uploadStorageMock.Verify(s => s.DeletePrefixAsync(It.IsAny<string>()), Times.Exactly(2));
        uploadStoreMock.Verify(s => s.RemoveUpload(staleUploadId1), Times.Once);
        uploadStoreMock.Verify(s => s.RemoveUpload(staleUploadId2), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesOncePerUploadWithMultipleFiles()
    {
        var staleUploadId = Guid.NewGuid();
        var staleTimestamp = DateTime.UtcNow.AddHours(-49);

        uploadStorageMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{staleUploadId}/file1.xtf", 1024, staleTimestamp),
                ($"uploads/{staleUploadId}/file2.xtf", 2048, staleTimestamp),
            });

        uploadStorageMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{staleUploadId}/"))
            .Returns(Task.CompletedTask);

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        uploadStorageMock.Verify(s => s.DeletePrefixAsync($"uploads/{staleUploadId}/"), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupAsyncHandlesEmptyBucket()
    {
        uploadStorageMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>());

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        uploadStorageMock.Verify(s => s.DeletePrefixAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesInvalidPrefixBlobs()
    {
        var staleTimestamp = DateTime.UtcNow.AddHours(-49);

        uploadStorageMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ("uploads/not-a-guid/file.xtf", 1024, staleTimestamp),
            });

        uploadStorageMock
            .Setup(s => s.DeleteAsync("uploads/not-a-guid/file.xtf"))
            .Returns(Task.CompletedTask);

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        uploadStorageMock.Verify(s => s.DeleteAsync("uploads/not-a-guid/file.xtf"), Times.Once);
        uploadStorageMock.Verify(s => s.DeletePrefixAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupAsyncOnlyDeletesStaleNotRecent()
    {
        var staleUploadId = Guid.NewGuid();
        var recentUploadId = Guid.NewGuid();

        uploadStorageMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{staleUploadId}/test.xtf", 1024, DateTime.UtcNow.AddHours(-49)),
                ($"uploads/{recentUploadId}/test.xtf", 1024, DateTime.UtcNow.AddHours(-1)),
            });

        uploadStorageMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{staleUploadId}/"))
            .Returns(Task.CompletedTask);

        uploadStoreMock.Setup(s => s.GetUpload(recentUploadId)).Returns(CreateUpload(recentUploadId));

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        uploadStorageMock.Verify(s => s.DeletePrefixAsync($"uploads/{staleUploadId}/"), Times.Once);
        uploadStorageMock.Verify(s => s.DeletePrefixAsync($"uploads/{recentUploadId}/"), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesOversizedFiles()
    {
        var uploadId = Guid.NewGuid();
        var oversizedBytes = ((long)cloudStorageOptions.MaxFileSizeMB * 1024 * 1024) + 1;

        uploadStorageMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{uploadId}/test.xtf", oversizedBytes, DateTime.UtcNow),
            });

        uploadStorageMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{uploadId}/"))
            .Returns(Task.CompletedTask);

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        uploadStorageMock.Verify(s => s.DeletePrefixAsync($"uploads/{uploadId}/"), Times.Once);
        uploadStoreMock.Verify(s => s.RemoveUpload(uploadId), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupAsyncSkipsNormalSizedFiles()
    {
        var uploadId = Guid.NewGuid();
        var normalBytes = (long)cloudStorageOptions.MaxFileSizeMB * 1024 * 1024;

        uploadStorageMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{uploadId}/test.xtf", normalBytes, DateTime.UtcNow),
            });

        uploadStoreMock.Setup(s => s.GetUpload(uploadId)).Returns(CreateUpload(uploadId));

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        uploadStorageMock.Verify(s => s.DeletePrefixAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesOrphanedUploadBlobs()
    {
        var orphanUploadId = Guid.NewGuid();

        uploadStorageMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ($"uploads/{orphanUploadId}/test.xtf", 1024, DateTime.UtcNow),
            });

        uploadStoreMock.Setup(s => s.GetUpload(orphanUploadId)).Returns((UploadInfo?)null);

        uploadStorageMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{orphanUploadId}/"))
            .Returns(Task.CompletedTask);

        SetupEmptyContainerListing();

        await service.RunCleanupAsync();

        uploadStorageMock.Verify(s => s.DeletePrefixAsync($"uploads/{orphanUploadId}/"), Times.Once);
    }

    [TestMethod]
    public async Task RunCleanupAsyncDeletesBlobsOutsideUploadsPrefix()
    {
        uploadStorageMock
            .Setup(s => s.ListFilesAsync("uploads/"))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>());

        uploadStorageMock
            .Setup(s => s.ListFilesAsync(string.Empty))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>
            {
                ("rogue-blob.txt", 512, DateTime.UtcNow),
                ("other/stuff.bin", 1024, DateTime.UtcNow),
                ("uploads/some-valid-path", 256, DateTime.UtcNow),
            });

        uploadStorageMock
            .Setup(s => s.DeleteAsync("rogue-blob.txt"))
            .Returns(Task.CompletedTask);
        uploadStorageMock
            .Setup(s => s.DeleteAsync("other/stuff.bin"))
            .Returns(Task.CompletedTask);

        await service.RunCleanupAsync();

        uploadStorageMock.Verify(s => s.DeleteAsync("rogue-blob.txt"), Times.Once);
        uploadStorageMock.Verify(s => s.DeleteAsync("other/stuff.bin"), Times.Once);
        uploadStorageMock.Verify(s => s.DeleteAsync("uploads/some-valid-path"), Times.Never);
    }

    private static UploadInfo CreateUpload(Guid uploadId) =>
        new UploadInfo(uploadId, ImmutableList<UploadedFileInfo>.Empty, DateTime.Now);

    private void SetupEmptyContainerListing()
    {
        uploadStorageMock
            .Setup(s => s.ListFilesAsync(string.Empty))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>());
    }
}
