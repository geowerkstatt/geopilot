using Geopilot.Api.Models;
using Geopilot.Api.Processing;
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
    private Mock<ILogger<AssetHandler>> loggerMock;
    private Mock<IProcessingService> validationServiceMock;
    private Mock<IUploadFileStore> uploadFileStoreMock;
    private Mock<IAssetFileStore> assetFileStoreMock;
    private AssetHandler assetHandler;
    private ProcessingJob job;
    private string uploadDirectory;
    private string assetDirectory;

    [TestInitialize]
    public void Initialize()
    {
        job = new ProcessingJob(Guid.NewGuid(), new List<ProcessingJobFile>() { new ProcessingJobFile("OriginalName", "TempFileName") }, null, DateTime.Now);
        uploadDirectory = AssemblyInitialize.TestDirectoryProvider.GetUploadDirectoryPath(job.Id);
        assetDirectory = AssemblyInitialize.TestDirectoryProvider.GetAssetDirectoryPath(job.Id);
        loggerMock = new Mock<ILogger<AssetHandler>>();
        validationServiceMock = new Mock<IProcessingService>();
        uploadFileStoreMock = new Mock<IUploadFileStore>();
        assetFileStoreMock = new Mock<IAssetFileStore>();
        assetHandler = new AssetHandler(loggerMock.Object, validationServiceMock.Object, uploadFileStoreMock.Object, assetFileStoreMock.Object, AssemblyInitialize.TestDirectoryProvider, new Mock<IContentTypeProvider>().Object);

        validationServiceMock.Setup(s => s.GetJob(job.Id)).Returns(job);
    }

    [TestMethod]
    public void PersistValidationJobAssetsShouldCopyPrimaryFiles()
    {
        var fileContent = "Some Content";
        Directory.CreateDirectory(uploadDirectory);
        File.WriteAllText(Path.Combine(uploadDirectory, "TempFileName"), fileContent);
        uploadFileStoreMock.Setup(x => x.OpenFile(job.Id, "TempFileName")).Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));
        uploadFileStoreMock.Setup(x => x.GetPath(job.Id, "TempFileName")).Returns(Path.Combine(uploadDirectory, "TempFileName"));

        Assert.IsFalse(Directory.Exists(assetDirectory));
        var assets = assetHandler.PersistJobAssets(job.Id);

        Assert.IsNotNull(assets);
        var primaryAsset = assets.FirstOrDefault(a => a.AssetType == AssetType.PrimaryData);
        Assert.IsNotNull(primaryAsset);
        Assert.AreEqual(AssetType.PrimaryData, primaryAsset.AssetType);
        Assert.AreEqual("TempFileName", primaryAsset.SanitizedFilename);
        Assert.AreEqual("OriginalName", primaryAsset.OriginalFilename);
        Assert.AreEqual(fileContent, File.ReadAllText(Path.Combine(assetDirectory, "TempFileName")));
        CollectionAssert.AreEquivalent(SHA256.HashData(Encoding.UTF8.GetBytes(fileContent)), primaryAsset.FileHash);
    }

    [TestMethod]
    public void PersistValidationJobAssetsRecordsStepDeliveryFilesInPlace()
    {
        var fileContent = "Some Content";
        Directory.CreateDirectory(uploadDirectory);
        Directory.CreateDirectory(assetDirectory);

        File.WriteAllText(Path.Combine(uploadDirectory, "TempFileName"), fileContent);
        uploadFileStoreMock.Setup(x => x.OpenFile(job.Id, "TempFileName")).Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));
        uploadFileStoreMock.Setup(x => x.GetPath(job.Id, "TempFileName")).Returns(Path.Combine(uploadDirectory, "TempFileName"));

        // Step delivery files were written directly into the asset store by the pipeline
        // runner, so the handler should hash them in place — no copy.
        File.WriteAllText(Path.Combine(assetDirectory, "mylogfile"), fileContent);
        assetFileStoreMock.Setup(x => x.OpenFile(job.Id, "mylogfile")).Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));

        var jobWithDownloads = new ProcessingJob(job.Id, new List<ProcessingJobFile> { new ProcessingJobFile("OriginalName", "TempFileName") }, null, DateTime.Now)
        {
            Pipeline = BuildPipelineWithDeliveryFiles("myStep", new List<PersistedFile> { new PersistedFile("mylogfile.log", "mylogfile") }),
        };
        validationServiceMock.Setup(s => s.GetJob(job.Id)).Returns(jobWithDownloads);

        var assets = assetHandler.PersistJobAssets(job.Id);

        Assert.IsTrue(File.Exists(Path.Combine(assetDirectory, "mylogfile")));
        var logfileAsset = assets.FirstOrDefault(a => a.AssetType == AssetType.ValidationReport);
        Assert.IsNotNull(logfileAsset);
        Assert.AreEqual(AssetType.ValidationReport, logfileAsset.AssetType);
        Assert.AreEqual("mylogfile", logfileAsset.SanitizedFilename);
        Assert.AreEqual("myStep_mylogfile.log", logfileAsset.OriginalFilename);
        Assert.AreEqual(fileContent, File.ReadAllText(Path.Combine(assetDirectory, "mylogfile")));
        CollectionAssert.AreEquivalent(SHA256.HashData(Encoding.UTF8.GetBytes(fileContent)), logfileAsset.FileHash);

        // The handler must not call GetPath on the asset store — there's no copy step for delivery files.
        assetFileStoreMock.Verify(x => x.GetPath(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public void PersistValidationJobAssetsFailsWithoutJobDirectory()
    {
        var jobWithDownloads = new ProcessingJob(job.Id, new List<ProcessingJobFile> { new ProcessingJobFile("OriginalName", "TempFileName") }, null, DateTime.Now)
        {
            Pipeline = BuildPipelineWithDeliveryFiles("myStep", new List<PersistedFile> { new PersistedFile("mylogfile.log", "mylogfile") }),
        };
        validationServiceMock.Setup(s => s.GetJob(job.Id)).Returns(jobWithDownloads);
        uploadFileStoreMock.Setup(x => x.OpenFile(job.Id, "TempFileName")).Throws(new FileNotFoundException());

        Assert.ThrowsExactly<FileNotFoundException>(() => assetHandler.PersistJobAssets(job.Id));
    }

    private static IPipeline BuildPipelineWithDeliveryFiles(string stepId, List<PersistedFile> deliveryFiles)
    {
        var stepMock = new Mock<IPipelineStep>();
        stepMock.SetupGet(s => s.Id).Returns(stepId);
        stepMock.SetupGet(s => s.DeliveryFiles).Returns(deliveryFiles);

        var pipelineMock = new Mock<IPipeline>();
        pipelineMock.SetupGet(p => p.Steps).Returns(new List<IPipelineStep> { stepMock.Object });
        return pipelineMock.Object;
    }

    [TestMethod]
    public void PersistValidationJobAssetsFailsWithoutJobFiles()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => assetHandler.PersistJobAssets(Guid.NewGuid()));
    }

    [TestMethod]
    public void DeleteJobAssets()
    {
        Directory.CreateDirectory(assetDirectory);
        File.WriteAllText(Path.Combine(assetDirectory, "TempFileName"), "Some Content");
        assetHandler.DeleteJobAssets(job.Id);
        Assert.IsFalse(Directory.Exists(assetDirectory));
    }
}
