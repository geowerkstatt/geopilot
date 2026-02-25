using Geopilot.Api.Contracts;
using Geopilot.Api.Enums;
using Geopilot.Api.Exceptions;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Services;
using Geopilot.Api.Validation;
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
    private Mock<IFileProvider> fileProviderMock;
    private Mock<IOptions<CloudStorageOptions>> optionsMock;
    private Mock<ILogger<CloudOrchestrationService>> loggerMock;
    private ValidationJobStore jobStore;
    private CloudOrchestrationService service;

    [TestInitialize]
    public void Initialize()
    {
        cloudStorageServiceMock = new Mock<ICloudStorageService>(MockBehavior.Strict);
        cloudScanServiceMock = new Mock<ICloudScanService>(MockBehavior.Strict);
        fileProviderMock = new Mock<IFileProvider>(MockBehavior.Strict);
        loggerMock = new Mock<ILogger<CloudOrchestrationService>>();

        optionsMock = new Mock<IOptions<CloudStorageOptions>>();
        optionsMock.SetupGet(o => o.Value).Returns(new CloudStorageOptions());

        jobStore = new ValidationJobStore();

        service = new CloudOrchestrationService(
            cloudStorageServiceMock.Object,
            cloudScanServiceMock.Object,
            jobStore,
            fileProviderMock.Object,
            optionsMock.Object,
            loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        cloudStorageServiceMock.VerifyAll();
        cloudScanServiceMock.VerifyAll();
        fileProviderMock.VerifyAll();
    }

    [TestMethod]
    public async Task InitiateUploadAsyncCreatesJobAndReturnsPresignedUrls()
    {
        var request = new CloudUploadRequest { Files = [new FileMetadata("test.xtf", 1024)] };

        cloudStorageServiceMock
            .Setup(s => s.GeneratePresignedUploadUrlAsync(It.IsAny<string>(), null, It.IsAny<TimeSpan>()))
            .ReturnsAsync("https://storage.example.com/presigned-url");

        var response = await service.InitiateUploadAsync(request);

        Assert.IsNotNull(response);
        Assert.AreNotEqual(Guid.Empty, response.JobId);
        Assert.HasCount(1, response.Files);
        Assert.AreEqual("test.xtf", response.Files[0].FileName);
        Assert.AreEqual("https://storage.example.com/presigned-url", response.Files[0].UploadUrl);

        var job = jobStore.GetJob(response.JobId);
        Assert.IsNotNull(job);
        Assert.AreEqual(Status.Created, job.Status);
        Assert.AreEqual(UploadMethod.Cloud, job.UploadMethod);
        Assert.IsNotNull(job.CloudFiles);
        Assert.HasCount(1, job.CloudFiles);
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

    // TODO: Remove this test when multi-file upload support is added.
    [TestMethod]
    public async Task InitiateUploadAsyncThrowsForMultipleFiles()
    {
        var request = new CloudUploadRequest
        {
            Files = [new FileMetadata("a.xtf", 1024), new FileMetadata("b.xtf", 2048)],
        };
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
        var maxBytes = (long)new CloudStorageOptions().MaxFileSizeMB * 1024 * 1024;
        var request = new CloudUploadRequest { Files = [new FileMetadata("test.xtf", maxBytes + 1)] };
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.InitiateUploadAsync(request));
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncSucceeds()
    {
        var job = CreateCloudJob("test.xtf", 1024);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size)> { ($"uploads/{job.Id}/test.xtf", 1024) });

        cloudScanServiceMock
            .Setup(s => s.CheckFilesAsync(It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(new ScanResult(true));

        await service.RunPreflightChecksAsync(job.Id);

        var updatedJob = jobStore.GetJob(job.Id);
        Assert.IsNotNull(updatedJob);
        Assert.AreEqual(Status.VerifyingUpload, updatedJob.Status);
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForMissingFile()
    {
        var job = CreateCloudJob("test.xtf", 1024);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size)>());

        var ex = await Assert.ThrowsExactlyAsync<CloudUploadPreflightException>(() => service.RunPreflightChecksAsync(job.Id));
        Assert.AreEqual(PreflightFailureReason.IncompleteUpload, ex.FailureReason);
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForIncompleteFile()
    {
        var job = CreateCloudJob("test.xtf", 1024);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size)> { ($"uploads/{job.Id}/test.xtf", 512) });

        var ex = await Assert.ThrowsExactlyAsync<CloudUploadPreflightException>(() => service.RunPreflightChecksAsync(job.Id));
        Assert.AreEqual(PreflightFailureReason.IncompleteUpload, ex.FailureReason);
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForThreatDetected()
    {
        var job = CreateCloudJob("test.xtf", 1024);

        cloudStorageServiceMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size)> { ($"uploads/{job.Id}/test.xtf", 1024) });

        cloudScanServiceMock
            .Setup(s => s.CheckFilesAsync(It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(new ScanResult(false, "Malware found"));

        cloudStorageServiceMock
            .Setup(s => s.DeletePrefixAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var ex = await Assert.ThrowsExactlyAsync<CloudUploadPreflightException>(() => service.RunPreflightChecksAsync(job.Id));
        Assert.AreEqual(PreflightFailureReason.ThreatDetected, ex.FailureReason);

        Assert.IsNull(jobStore.GetJob(job.Id));
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForUnknownJob()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RunPreflightChecksAsync(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForDirectUploadJob()
    {
        var job = jobStore.CreateJob();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.RunPreflightChecksAsync(job.Id));
    }

    [TestMethod]
    public async Task StageFilesLocallyAsyncDownloadsAndSetsFileInfo()
    {
        var job = CreateCloudJob("test.xtf", 1024);

        fileProviderMock.Setup(f => f.Initialize(job.Id));
        using var stream = new MemoryStream();
        using var fileHandle = new FileHandle("random123.xtf", stream);
        fileProviderMock
            .Setup(f => f.CreateFileWithRandomName(".xtf"))
            .Returns(fileHandle);

        cloudStorageServiceMock
            .Setup(s => s.DownloadAsync($"uploads/{job.Id}/test.xtf", It.IsAny<Stream>()))
            .Returns(Task.CompletedTask);

        var updated = await service.StageFilesLocallyAsync(job.Id);

        Assert.AreEqual("test.xtf", updated.OriginalFileName);
        Assert.AreEqual("random123.xtf", updated.TempFileName);
        Assert.AreEqual(Status.Ready, updated.Status);
    }

    [TestMethod]
    public async Task StageFilesLocallyAsyncThrowsForUnknownJob()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.StageFilesLocallyAsync(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task StageFilesLocallyAsyncThrowsForDirectUploadJob()
    {
        var job = jobStore.CreateJob();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.StageFilesLocallyAsync(job.Id));
    }

    private ValidationJob CreateCloudJob(string fileName, long size)
    {
        var job = jobStore.CreateJob();
        var cloudFiles = ImmutableList.Create(new CloudFileInfo(fileName, $"uploads/{job.Id}/{fileName}", size));
        jobStore.AddUploadInfoToJob(job.Id, UploadMethod.Cloud, cloudFiles);
        return jobStore.GetJob(job.Id)!;
    }
}
