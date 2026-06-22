using Geopilot.Api.Contracts;
using Geopilot.Api.Enums;
using Geopilot.Api.Exceptions;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Immutable;

namespace Geopilot.Api.Test.Services;

[TestClass]
public class CloudOrchestrationServiceTest
{
    private Mock<ICloudStorageService> cloudStorageServiceMock;
    private Mock<ICloudScanService> cloudScanServiceMock;
    private Mock<IUploadFileStore> uploadFileStoreMock;
    private Mock<IOptions<CloudStorageOptions>> optionsMock;
    private Mock<ILogger<CloudOrchestrationService>> loggerMock;
    private ProcessingJobStore jobStore;
    private UploadStore uploadStore;
    private CloudOrchestrationService service;

    [TestInitialize]
    public void Initialize()
    {
        cloudStorageServiceMock = new Mock<ICloudStorageService>(MockBehavior.Strict);
        cloudScanServiceMock = new Mock<ICloudScanService>(MockBehavior.Strict);
        uploadFileStoreMock = new Mock<IUploadFileStore>(MockBehavior.Strict);
        loggerMock = new Mock<ILogger<CloudOrchestrationService>>();

        optionsMock = new Mock<IOptions<CloudStorageOptions>>();
        optionsMock.SetupGet(o => o.Value).Returns(new CloudStorageOptions
        {
            MaxFileSizeMB = 2048,
            MaxFilesPerJob = 12,
            MaxJobSizeMB = 10240,
            MaxGlobalActiveSizeMB = 204800,
            MaxActiveJobs = 100,
            PresignedUrlExpiryMinutes = 60,
        });

        jobStore = new ProcessingJobStore();
        uploadStore = new UploadStore();

        service = new CloudOrchestrationService(
            cloudStorageServiceMock.Object,
            cloudScanServiceMock.Object,
            jobStore,
            uploadStore,
            uploadFileStoreMock.Object,
            optionsMock.Object,
            loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        cloudStorageServiceMock.VerifyAll();
        cloudScanServiceMock.VerifyAll();
        uploadFileStoreMock.VerifyAll();
    }

    [TestMethod]
    public async Task InitiateUploadAsyncCreatesUploadAndReturnsPresignedUrls()
    {
        var request = new CloudUploadRequest { Files = [new FileMetadata("test.xtf", 1024)] };

        SetupGlobalLimitChecks();

        cloudStorageServiceMock
            .Setup(s => s.GeneratePresignedUploadUrlAsync(It.IsAny<string>(), null, It.IsAny<TimeSpan>()))
            .ReturnsAsync("https://storage.example.com/presigned-url");

        var response = await service.InitiateUploadAsync(request);

        Assert.IsNotNull(response);
        Assert.AreNotEqual(Guid.Empty, response.UploadId);
        Assert.HasCount(1, response.Files);
        Assert.AreEqual("test.xtf", response.Files[0].FileName);
        Assert.AreEqual("https://storage.example.com/presigned-url", response.Files[0].UploadUrl);

        var upload = uploadStore.GetUpload(response.UploadId);
        Assert.IsNotNull(upload);
        Assert.HasCount(1, upload.Files);
        Assert.AreEqual("test.xtf", upload.Files[0].FileName);
    }

    [TestMethod]
    public async Task InitiateUploadAsyncThrowsForNullRequest()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => service.InitiateUploadAsync(null!));
    }

    [TestMethod]
    public async Task InitiateUploadAsyncThrowsForEmptyFiles()
    {
        var request = new CloudUploadRequest { Files = [] };
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.InitiateUploadAsync(request));
    }

    [TestMethod]
    public async Task InitiateUploadAsyncThrowsForZeroFileSize()
    {
        var request = new CloudUploadRequest { Files = [new FileMetadata("test.xtf", 0)] };
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.InitiateUploadAsync(request));
    }

    [TestMethod]
    public async Task InitiateUploadAsyncThrowsForNegativeFileSize()
    {
        var request = new CloudUploadRequest { Files = [new FileMetadata("test.xtf", -1)] };
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.InitiateUploadAsync(request));
    }

    [TestMethod]
    public async Task InitiateUploadAsyncThrowsForOversizedFile()
    {
        var maxBytes = (long)optionsMock.Object.Value.MaxFileSizeMB * 1024 * 1024;
        var request = new CloudUploadRequest { Files = [new FileMetadata("test.xtf", maxBytes + 1)] };
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.InitiateUploadAsync(request));
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncSucceeds()
    {
        var upload = CreateUpload("test.xtf", 1024);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)> { ($"uploads/{upload.Id}/test.xtf", 1024, DateTime.UtcNow) });

        cloudScanServiceMock
            .Setup(s => s.CheckFilesAsync(It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(new ScanResult(true));

        await service.RunPreflightChecksAsync(upload.Id);

        Assert.IsNotNull(uploadStore.GetUpload(upload.Id));
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForMissingFile()
    {
        var upload = CreateUpload("test.xtf", 1024);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>());

        var ex = await Assert.ThrowsExactlyAsync<CloudUploadPreflightException>(() => service.RunPreflightChecksAsync(upload.Id));
        Assert.AreEqual(PreflightFailureReason.IncompleteUpload, ex.FailureReason);
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForIncompleteFile()
    {
        var upload = CreateUpload("test.xtf", 1024);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)> { ($"uploads/{upload.Id}/test.xtf", 512, DateTime.UtcNow) });

        var ex = await Assert.ThrowsExactlyAsync<CloudUploadPreflightException>(() => service.RunPreflightChecksAsync(upload.Id));
        Assert.AreEqual(PreflightFailureReason.IncompleteUpload, ex.FailureReason);
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForThreatDetected()
    {
        var upload = CreateUpload("test.xtf", 1024);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)> { ($"uploads/{upload.Id}/test.xtf", 1024, DateTime.UtcNow) });

        cloudScanServiceMock
            .Setup(s => s.CheckFilesAsync(It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(new ScanResult(false, "Malware found"));

        var ex = await Assert.ThrowsExactlyAsync<CloudUploadPreflightException>(() => service.RunPreflightChecksAsync(upload.Id));
        Assert.AreEqual(PreflightFailureReason.ThreatDetected, ex.FailureReason);
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForUnknownUpload()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RunPreflightChecksAsync(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task StageFilesLocallyAsyncDownloadsAndSetsFileInfo()
    {
        var upload = CreateUpload("test.xtf", 1024);
        var job = jobStore.CreateJob();

        uploadFileStoreMock.Setup(f => f.Exists(job.Id, "test.xtf")).Returns(false);
        uploadFileStoreMock
            .Setup(f => f.CreateFile(job.Id, "test.xtf"))
            .Returns(new MemoryStream());

        cloudStorageServiceMock
            .Setup(s => s.DownloadAsync($"uploads/{upload.Id}/test.xtf", It.IsAny<Stream>()))
            .Returns(Task.CompletedTask);

        cloudStorageServiceMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{upload.Id}/"))
            .Returns(Task.CompletedTask);

        var updated = await service.StageFilesLocallyAsync(upload.Id, job.Id);

        Assert.HasCount(1, updated.Files);
        Assert.AreEqual("test.xtf", updated.Files[0].OriginalFileName);
        Assert.AreEqual("test.xtf", updated.Files[0].TempFileName);
        cloudStorageServiceMock.Verify(s => s.DeletePrefixAsync($"uploads/{upload.Id}/"), Times.Once);
        Assert.IsNull(uploadStore.GetUpload(upload.Id));
    }

    [TestMethod]
    public async Task StageFilesLocallyAsyncThrowsForUnknownUpload()
    {
        var job = jobStore.CreateJob();
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.StageFilesLocallyAsync(Guid.NewGuid(), job.Id));
    }

    [TestMethod]
    public async Task StageFilesLocallyAsyncThrowsForUnknownJob()
    {
        var upload = CreateUpload("test.xtf", 1024);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.StageFilesLocallyAsync(upload.Id, Guid.NewGuid()));
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForOversizedFile()
    {
        var upload = CreateUpload("test.xtf", 1024);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)> { ($"uploads/{upload.Id}/test.xtf", 2048, DateTime.UtcNow) });

        var ex = await Assert.ThrowsExactlyAsync<CloudUploadPreflightException>(() => service.RunPreflightChecksAsync(upload.Id));
        Assert.AreEqual(PreflightFailureReason.SizeExceeded, ex.FailureReason);
    }

    [TestMethod]
    public async Task InitiateUploadAsyncSanitizesFileName()
    {
        var request = new CloudUploadRequest { Files = [new FileMetadata("../../etc/passwd", 1024)] };

        SetupGlobalLimitChecks();

        cloudStorageServiceMock
            .Setup(s => s.GeneratePresignedUploadUrlAsync(It.Is<string>(k => k.EndsWith("/passwd")), null, It.IsAny<TimeSpan>()))
            .ReturnsAsync("https://storage.example.com/presigned-url");

        var response = await service.InitiateUploadAsync(request);

        Assert.AreEqual("passwd", response.Files[0].FileName);

        var upload = uploadStore.GetUpload(response.UploadId);
        Assert.IsNotNull(upload);
        Assert.AreEqual("passwd", upload.Files[0].FileName);
        Assert.EndsWith("/passwd", upload.Files[0].CloudKey);
    }

    [TestMethod]
    public async Task InitiateUploadAsyncThrowsWhenMaxActiveUploadsReached()
    {
        var opts = new CloudStorageOptions { MaxFileSizeMB = 2048, MaxFilesPerJob = 12, MaxJobSizeMB = 10240, MaxActiveJobs = 1 };
        optionsMock.SetupGet(o => o.Value).Returns(opts);

        // Create one upload to hit the limit.
        uploadStore.CreateUpload(Guid.NewGuid(), ImmutableList.Create(new CloudFileInfo("f.xtf", "uploads/f.xtf", 100)));

        var request = new CloudUploadRequest { Files = [new FileMetadata("test.xtf", 1024)] };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.InitiateUploadAsync(request));
    }

    [TestMethod]
    public async Task InitiateUploadAsyncThrowsWhenGlobalSizeLimitExceeded()
    {
        var opts = new CloudStorageOptions { MaxFileSizeMB = 2048, MaxFilesPerJob = 12, MaxJobSizeMB = 10240, MaxActiveJobs = 100, MaxGlobalActiveSizeMB = 1 };
        optionsMock.SetupGet(o => o.Value).Returns(opts);

        cloudStorageServiceMock
            .Setup(s => s.GetTotalSizeAsync("uploads/"))
            .ReturnsAsync(1L * 1024 * 1024);

        var request = new CloudUploadRequest { Files = [new FileMetadata("test.xtf", 1024)] };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.InitiateUploadAsync(request));
    }

    private UploadInfo CreateUpload(string fileName, long size)
    {
        var uploadId = Guid.NewGuid();
        var cloudFiles = ImmutableList.Create(new CloudFileInfo(fileName, $"uploads/{uploadId}/{fileName}", size));
        return uploadStore.CreateUpload(uploadId, cloudFiles);
    }

    private void SetupGlobalLimitChecks()
    {
        cloudStorageServiceMock
            .Setup(s => s.GetTotalSizeAsync("uploads/"))
            .ReturnsAsync(0L);
    }
}
