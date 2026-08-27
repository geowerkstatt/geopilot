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
public class UploadOrchestrationServiceTest
{
    private Mock<IUploadStorage> uploadStorageMock;
    private Mock<IUploadScanService> scanServiceMock;
    private Mock<IDirectoryProvider> directoryProviderMock;
    private Mock<IOptions<UploadOptions>> optionsMock;
    private Mock<ILogger<UploadOrchestrationService>> loggerMock;
    private ProcessingJobStore jobStore;
    private UploadStore uploadStore;
    private UploadOrchestrationService service;
    private string pipelineRoot;

    [TestInitialize]
    public void Initialize()
    {
        uploadStorageMock = new Mock<IUploadStorage>(MockBehavior.Strict);
        scanServiceMock = new Mock<IUploadScanService>(MockBehavior.Strict);

        // Own root per test instance: the methods of this class run in parallel, so a shared root
        // would have one test's cleanup delete files another test is still writing.
        pipelineRoot = Path.Combine(Path.GetTempPath(), "UploadOrchestrationServiceTest_" + Guid.NewGuid());
        directoryProviderMock = new Mock<IDirectoryProvider>();
        directoryProviderMock
            .Setup(d => d.GetPipelineDirectoryPath(It.IsAny<Guid>()))
            .Returns((Guid jobId) => Path.Combine(pipelineRoot, jobId.ToString()));
        loggerMock = new Mock<ILogger<UploadOrchestrationService>>();

        optionsMock = new Mock<IOptions<UploadOptions>>();
        optionsMock.SetupGet(o => o.Value).Returns(new UploadOptions
        {
            MaxFileSizeMB = 2048,
            MaxFilesPerJob = 12,
            MaxJobSizeMB = 10240,
            MaxGlobalActiveSizeMB = 204800,
            MaxActiveJobs = 100,
            UploadUrlExpiryMinutes = 60,
        });

        jobStore = new ProcessingJobStore();
        uploadStore = new UploadStore();

        service = new UploadOrchestrationService(
            uploadStorageMock.Object,
            scanServiceMock.Object,
            jobStore,
            uploadStore,
            directoryProviderMock.Object,
            optionsMock.Object,
            loggerMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        uploadStorageMock.VerifyAll();
        scanServiceMock.VerifyAll();

        if (Directory.Exists(pipelineRoot))
            Directory.Delete(pipelineRoot, recursive: true);
    }

    [TestMethod]
    public async Task InitiateUploadAsyncCreatesUploadAndReturnsPresignedUrls()
    {
        var request = new InitiateUploadRequest { Files = [new FileMetadata("test.xtf", 1024)] };

        SetupGlobalLimitChecks();

        uploadStorageMock
            .Setup(s => s.GenerateUploadUrlAsync(It.IsAny<string>(), null, It.IsAny<TimeSpan>()))
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
        var request = new InitiateUploadRequest { Files = [] };
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.InitiateUploadAsync(request));
    }

    [TestMethod]
    public async Task InitiateUploadAsyncThrowsForZeroFileSize()
    {
        var request = new InitiateUploadRequest { Files = [new FileMetadata("test.xtf", 0)] };
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.InitiateUploadAsync(request));
    }

    [TestMethod]
    public async Task InitiateUploadAsyncThrowsForNegativeFileSize()
    {
        var request = new InitiateUploadRequest { Files = [new FileMetadata("test.xtf", -1)] };
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.InitiateUploadAsync(request));
    }

    [TestMethod]
    public async Task InitiateUploadAsyncThrowsForOversizedFile()
    {
        var maxBytes = (long)optionsMock.Object.Value.MaxFileSizeMB * 1024 * 1024;
        var request = new InitiateUploadRequest { Files = [new FileMetadata("test.xtf", maxBytes + 1)] };
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.InitiateUploadAsync(request));
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncSucceeds()
    {
        var upload = CreateUpload("test.xtf", 1024);

        uploadStorageMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)> { ($"uploads/{upload.Id}/test.xtf", 1024, DateTime.UtcNow) });

        var scanResult = new ScanResult(true, Hashes: new Dictionary<string, string> { [$"uploads/{upload.Id}/test.xtf"] = "abc123" });
        scanServiceMock
            .Setup(s => s.CheckFilesAsync(It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(scanResult);

        var result = await service.RunPreflightChecksAsync(upload.Id);

        Assert.IsNotNull(uploadStore.GetUpload(upload.Id));
        Assert.AreSame(scanResult, result, "the scan outcome must reach the caller so the protocol can record it.");
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForMissingFile()
    {
        var upload = CreateUpload("test.xtf", 1024);

        uploadStorageMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)>());

        var ex = await Assert.ThrowsExactlyAsync<UploadPreflightException>(() => service.RunPreflightChecksAsync(upload.Id));
        Assert.AreEqual(PreflightFailureReason.IncompleteUpload, ex.FailureReason);
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForIncompleteFile()
    {
        var upload = CreateUpload("test.xtf", 1024);

        uploadStorageMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)> { ($"uploads/{upload.Id}/test.xtf", 512, DateTime.UtcNow) });

        var ex = await Assert.ThrowsExactlyAsync<UploadPreflightException>(() => service.RunPreflightChecksAsync(upload.Id));
        Assert.AreEqual(PreflightFailureReason.IncompleteUpload, ex.FailureReason);
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForThreatDetected()
    {
        var upload = CreateUpload("test.xtf", 1024);

        uploadStorageMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)> { ($"uploads/{upload.Id}/test.xtf", 1024, DateTime.UtcNow) });

        var scanResult = new ScanResult(false, "Malware found");
        scanServiceMock
            .Setup(s => s.CheckFilesAsync(It.IsAny<IReadOnlyList<string>>()))
            .ReturnsAsync(scanResult);

        var ex = await Assert.ThrowsExactlyAsync<UploadPreflightException>(() => service.RunPreflightChecksAsync(upload.Id));
        Assert.AreEqual(PreflightFailureReason.ThreatDetected, ex.FailureReason);
        Assert.AreSame(scanResult, ex.ScanResult, "a detected threat must carry the scan outcome so the protocol can record it.");
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForUnknownUpload()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.RunPreflightChecksAsync(Guid.NewGuid()));
    }

    [TestMethod]
    public void RegisterJobFilesRecordsFilesWithoutTransferringAnything()
    {
        var upload = CreateUpload("test.xtf", 1024);
        var job = jobStore.CreateJob(upload.Id);

        var files = service.RegisterJobFiles(upload.Id, job.Id);

        Assert.HasCount(1, files);
        Assert.AreEqual("test.xtf", files[0].OriginalFileName);
        Assert.IsInstanceOfType<UploadPipelineFile>(files[0]);

        // The strict storage mock has no setup at all, so any transfer or deletion would have thrown.
        Assert.IsNotNull(uploadStore.GetUpload(upload.Id), "the upload must stay available until the job is done with it");
    }

    [TestMethod]
    public async Task RegisterJobFilesMaterializesInsideTheJobsPipelineDirectory()
    {
        var upload = CreateUpload("test.xtf", 1024);
        var job = jobStore.CreateJob(upload.Id);

        uploadStorageMock
            .Setup(s => s.DownloadAsync($"uploads/{upload.Id}/test.xtf", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var files = service.RegisterJobFiles(upload.Id, job.Id);
        var materializedPath = await files[0].GetLocalPathAsync();

        // The fetched file has to land inside the job's pipeline directory, because that is what
        // Pipeline.Dispose removes at the end of the run. Anywhere else and the uploads pile up.
        var jobPipelineDirectory = directoryProviderMock.Object.GetPipelineDirectoryPath(job.Id);
        Assert.IsTrue(
            materializedPath.StartsWith(jobPipelineDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal),
            $"expected <{materializedPath}> below <{jobPipelineDirectory}>");

        // Shared by every step, so not below any single step's working directory.
        Assert.AreEqual(job.Id.ToString(), new DirectoryInfo(materializedPath).Parent?.Parent?.Name);
    }

    [TestMethod]
    public async Task RegisterJobFilesDisambiguatesFilesThatSanitizeToTheSameName()
    {
        var upload = CreateUpload(("a:b.xtf", 1024), ("a*b.xtf", 2048));
        var job = jobStore.CreateJob(upload.Id);
        uploadStorageMock
            .Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var registered = service.RegisterJobFiles(upload.Id, job.Id);

        // Sanitizing drops the invalid characters, so both names collapse to the same one.
        Assert.HasCount(2, registered);
        Assert.AreEqual("ab.xtf", Path.GetFileName(await registered[0].GetLocalPathAsync(default)));
        Assert.AreEqual("ab_2.xtf", Path.GetFileName(await registered[1].GetLocalPathAsync(default)));

        uploadStorageMock
            .Verify(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task RegisterJobFilesDisambiguatesNamesThatDifferOnlyInCase()
    {
        var upload = CreateUpload(("Data.xtf", 1024), ("data.xtf", 2048));
        var job = jobStore.CreateJob(upload.Id);
        uploadStorageMock
            .Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var registered = service.RegisterJobFiles(upload.Id, job.Id);

        // The name is used as a file name twice, when materializing and in the asset store. On Windows and
        // macOS both spellings address the same file, so without disambiguation the second upload would
        // overwrite the first in the pipeline run and in the delivery.
        Assert.HasCount(2, registered);
        Assert.AreEqual("Data.xtf", Path.GetFileName(await registered[0].GetLocalPathAsync(default)));
        Assert.AreEqual("data_2.xtf", Path.GetFileName(await registered[1].GetLocalPathAsync(default)));

        uploadStorageMock
            .Verify(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public void RegisterJobFilesThrowsForUnknownUpload()
    {
        var job = jobStore.CreateJob(Guid.NewGuid());
        Assert.ThrowsExactly<ArgumentException>(() => service.RegisterJobFiles(Guid.NewGuid(), job.Id));
    }

    [TestMethod]
    public void RegisterJobFilesThrowsForUnknownJob()
    {
        var upload = CreateUpload("test.xtf", 1024);
        Assert.ThrowsExactly<ArgumentException>(() => service.RegisterJobFiles(upload.Id, Guid.NewGuid()));
    }

    [TestMethod]
    public async Task ReleaseUploadAsyncDeletesThePrefixAndForgetsTheUpload()
    {
        var upload = CreateUpload("test.xtf", 1024);

        uploadStorageMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{upload.Id}/"))
            .Returns(Task.CompletedTask);

        await service.ReleaseUploadAsync(upload.Id);

        uploadStorageMock.Verify(s => s.DeletePrefixAsync($"uploads/{upload.Id}/"), Times.Once);
        Assert.IsNull(uploadStore.GetUpload(upload.Id));
    }

    [TestMethod]
    public async Task ReleaseUploadAsyncSwallowsStorageFailures()
    {
        var upload = CreateUpload("test.xtf", 1024);

        uploadStorageMock
            .Setup(s => s.DeletePrefixAsync($"uploads/{upload.Id}/"))
            .ThrowsAsync(new InvalidOperationException("storage down"));

        // The caller is in a cleanup path; a failed release is picked up by the age-based sweep instead.
        await service.ReleaseUploadAsync(upload.Id);
    }

    [TestMethod]
    public async Task RunPreflightChecksAsyncThrowsForOversizedFile()
    {
        var upload = CreateUpload("test.xtf", 1024);

        uploadStorageMock
            .Setup(s => s.ListFilesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<(string Key, long Size, DateTime LastModified)> { ($"uploads/{upload.Id}/test.xtf", 2048, DateTime.UtcNow) });

        var ex = await Assert.ThrowsExactlyAsync<UploadPreflightException>(() => service.RunPreflightChecksAsync(upload.Id));
        Assert.AreEqual(PreflightFailureReason.SizeExceeded, ex.FailureReason);
    }

    [TestMethod]
    public async Task InitiateUploadAsyncSanitizesFileName()
    {
        var request = new InitiateUploadRequest { Files = [new FileMetadata("../../etc/passwd", 1024)] };

        SetupGlobalLimitChecks();

        uploadStorageMock
            .Setup(s => s.GenerateUploadUrlAsync(It.Is<string>(k => k.EndsWith("/passwd")), null, It.IsAny<TimeSpan>()))
            .ReturnsAsync("https://storage.example.com/presigned-url");

        var response = await service.InitiateUploadAsync(request);

        Assert.AreEqual("passwd", response.Files[0].FileName);

        var upload = uploadStore.GetUpload(response.UploadId);
        Assert.IsNotNull(upload);
        Assert.AreEqual("passwd", upload.Files[0].FileName);
        Assert.EndsWith("/passwd", upload.Files[0].StorageKey);
    }

    [TestMethod]
    public async Task InitiateUploadAsyncThrowsWhenMaxActiveUploadsReached()
    {
        var opts = new UploadOptions { MaxFileSizeMB = 2048, MaxFilesPerJob = 12, MaxJobSizeMB = 10240, MaxActiveJobs = 1 };
        optionsMock.SetupGet(o => o.Value).Returns(opts);

        // Create one upload to hit the limit.
        uploadStore.CreateUpload(Guid.NewGuid(), ImmutableList.Create(new UploadedFileInfo("f.xtf", "uploads/f.xtf", 100)));

        var request = new InitiateUploadRequest { Files = [new FileMetadata("test.xtf", 1024)] };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.InitiateUploadAsync(request));
    }

    [TestMethod]
    public async Task InitiateUploadAsyncThrowsWhenGlobalSizeLimitExceeded()
    {
        var opts = new UploadOptions { MaxFileSizeMB = 2048, MaxFilesPerJob = 12, MaxJobSizeMB = 10240, MaxActiveJobs = 100, MaxGlobalActiveSizeMB = 1 };
        optionsMock.SetupGet(o => o.Value).Returns(opts);

        uploadStorageMock
            .Setup(s => s.GetTotalSizeAsync("uploads/"))
            .ReturnsAsync(1L * 1024 * 1024);

        var request = new InitiateUploadRequest { Files = [new FileMetadata("test.xtf", 1024)] };

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.InitiateUploadAsync(request));
    }

    private UploadInfo CreateUpload(string fileName, long size)
        => CreateUpload((fileName, size));

    private UploadInfo CreateUpload(params (string FileName, long Size)[] files)
    {
        var uploadId = Guid.NewGuid();
        var uploadedFiles = files
            .Select(f => new UploadedFileInfo(f.FileName, $"uploads/{uploadId}/{f.FileName}", f.Size))
            .ToImmutableList();
        return uploadStore.CreateUpload(uploadId, uploadedFiles);
    }

    private void SetupGlobalLimitChecks()
    {
        uploadStorageMock
            .Setup(s => s.GetTotalSizeAsync("uploads/"))
            .ReturnsAsync(0L);
    }
}
