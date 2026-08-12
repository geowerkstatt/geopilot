using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace Geopilot.Api.FileAccess;

[TestClass]
public class AssetHandlerTest
{
    private const string FileContent = "Some Content";

    private Mock<ILogger<AssetHandler>> loggerMock;
    private Mock<IProcessingService> validationServiceMock;
    private Mock<ICloudStorageService> cloudStorageServiceMock;
    private Mock<IAssetFileStore> assetFileStoreMock;
    private AssetHandler assetHandler;
    private ProcessingJob job;
    private string assetDirectory;

    [TestInitialize]
    public void Initialize()
    {
        job = CreateJob();
        assetDirectory = AssemblyInitialize.TestDirectoryProvider.GetAssetDirectoryPath(job.Id);
        loggerMock = new Mock<ILogger<AssetHandler>>();
        validationServiceMock = new Mock<IProcessingService>();
        cloudStorageServiceMock = new Mock<ICloudStorageService>();
        assetFileStoreMock = new Mock<IAssetFileStore>();
        assetHandler = new AssetHandler(loggerMock.Object, validationServiceMock.Object, cloudStorageServiceMock.Object, assetFileStoreMock.Object, AssemblyInitialize.TestDirectoryProvider, new Mock<IContentTypeProvider>().Object);

        validationServiceMock.Setup(s => s.GetJob(job.Id)).Returns(job);
    }

    [TestMethod]
    public async Task PersistValidationJobAssetsFetchesPrimaryFilesFromCloudStorage()
    {
        SetupCloudFile("uploads/key/OriginalName", FileContent);
        SetupAssetStoreWritesToDisk();

        Assert.IsFalse(Directory.Exists(assetDirectory));
        var assets = await assetHandler.PersistJobAssetsAsync(job.Id);

        Assert.IsNotNull(assets);
        var primaryAsset = assets.FirstOrDefault(a => a.AssetType == AssetType.PrimaryData);
        Assert.IsNotNull(primaryAsset);
        Assert.AreEqual(AssetType.PrimaryData, primaryAsset.AssetType);
        Assert.AreEqual("TempFileName", primaryAsset.SanitizedFilename);
        Assert.AreEqual("OriginalName", primaryAsset.OriginalFilename);
        Assert.AreEqual(FileContent, File.ReadAllText(Path.Combine(assetDirectory, "TempFileName")));
        CollectionAssert.AreEquivalent(SHA256.HashData(Encoding.UTF8.GetBytes(FileContent)), primaryAsset.FileHash);
        cloudStorageServiceMock.Verify(x => x.OpenReadAsync("uploads/key/OriginalName", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task PersistValidationJobAssetsRecordsStepDeliveryFilesInPlace()
    {
        Directory.CreateDirectory(assetDirectory);
        SetupCloudFile("uploads/key/OriginalName", FileContent);
        SetupAssetStoreWritesToDisk();

        // Step delivery files were written directly into the asset store by the pipeline
        // runner, so the handler should hash them in place, no copy.
        File.WriteAllText(Path.Combine(assetDirectory, "mylogfile"), FileContent);
        assetFileStoreMock.Setup(x => x.OpenFile(job.Id, "mylogfile")).Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(FileContent)));

        var jobWithDownloads = CreateJob(job.Id) with
        {
            Pipeline = BuildPipelineWithDeliveryFiles("myStep", new List<PersistedFile> { new PersistedFile("mylogfile.log", "mylogfile") }),
        };
        validationServiceMock.Setup(s => s.GetJob(job.Id)).Returns(jobWithDownloads);

        var assets = await assetHandler.PersistJobAssetsAsync(job.Id);

        Assert.IsTrue(File.Exists(Path.Combine(assetDirectory, "mylogfile")));
        var logfileAsset = assets.FirstOrDefault(a => a.AssetType == AssetType.ValidationReport);
        Assert.IsNotNull(logfileAsset);
        Assert.AreEqual(AssetType.ValidationReport, logfileAsset.AssetType);
        Assert.AreEqual("mylogfile", logfileAsset.SanitizedFilename);
        Assert.AreEqual("myStep_mylogfile.log", logfileAsset.OriginalFilename);
        Assert.AreEqual(FileContent, File.ReadAllText(Path.Combine(assetDirectory, "mylogfile")));
        CollectionAssert.AreEquivalent(SHA256.HashData(Encoding.UTF8.GetBytes(FileContent)), logfileAsset.FileHash);

        // The handler must not call GetPath on the asset store — there's no copy step for delivery files.
        assetFileStoreMock.Verify(x => x.GetPath(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task PersistValidationJobAssetsFailsWhenTheUploadIsGone()
    {
        var jobWithDownloads = CreateJob(job.Id) with
        {
            Pipeline = BuildPipelineWithDeliveryFiles("myStep", new List<PersistedFile> { new PersistedFile("mylogfile.log", "mylogfile") }),
        };
        validationServiceMock.Setup(s => s.GetJob(job.Id)).Returns(jobWithDownloads);
        cloudStorageServiceMock
            .Setup(x => x.OpenReadAsync("uploads/key/OriginalName", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());

        await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => assetHandler.PersistJobAssetsAsync(job.Id));
    }

    [TestMethod]
    public async Task PersistValidationJobAssetsFailsWithoutJobFiles()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => assetHandler.PersistJobAssetsAsync(Guid.NewGuid()));
    }

    [TestMethod]
    public void DeleteJobAssets()
    {
        Directory.CreateDirectory(assetDirectory);
        File.WriteAllText(Path.Combine(assetDirectory, "TempFileName"), FileContent);
        assetHandler.DeleteJobAssets(job.Id);
        Assert.IsFalse(Directory.Exists(assetDirectory));
    }

    private static ProcessingJob CreateJob(Guid? jobId = null)
        => new ProcessingJob(
            jobId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            new List<ProcessingJobFile> { new ProcessingJobFile("OriginalName", "TempFileName", "uploads/key/OriginalName") },
            null,
            DateTime.Now);

    private static IPipeline BuildPipelineWithDeliveryFiles(string stepId, List<PersistedFile> deliveryFiles)
    {
        var stepMock = new Mock<IPipelineStep>();
        stepMock.SetupGet(s => s.Id).Returns(stepId);
        stepMock.SetupGet(s => s.DeliveryFiles).Returns(deliveryFiles);

        var pipelineMock = new Mock<IPipeline>();
        pipelineMock.SetupGet(p => p.Steps).Returns(new List<IPipelineStep> { stepMock.Object });
        return pipelineMock.Object;
    }

    private void SetupCloudFile(string cloudKey, string content)
        => cloudStorageServiceMock
            .Setup(x => x.OpenReadAsync(cloudKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(content)));

    private void SetupAssetStoreWritesToDisk()
        => assetFileStoreMock
            .Setup(x => x.CreateFile(job.Id, It.IsAny<string>()))
            .Returns((Guid jobId, string fileName) =>
            {
                var directory = AssemblyInitialize.TestDirectoryProvider.GetAssetDirectoryPath(jobId);
                Directory.CreateDirectory(directory);
                return File.Create(Path.Combine(directory, fileName));
            });
}
