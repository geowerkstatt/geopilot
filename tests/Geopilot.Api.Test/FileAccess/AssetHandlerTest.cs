using Geopilot.Api.Models;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Processing;
using Geopilot.PipelineCore.Pipeline;
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
    private Mock<IFileProvider> fileProviderMock;
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
        fileProviderMock = new Mock<IFileProvider>();
        assetHandler = new AssetHandler(loggerMock.Object, validationServiceMock.Object, fileProviderMock.Object, AssemblyInitialize.TestDirectoryProvider, new Mock<IContentTypeProvider>().Object);

        validationServiceMock.Setup(s => s.GetJob(job.Id)).Returns(job);
    }

    [TestMethod]
    public void PersistValidationJobAssetsShouldCopyPrimaryFiles()
    {
        var fileContent = "Some Content";
        Directory.CreateDirectory(uploadDirectory);
        File.WriteAllText(Path.Combine(uploadDirectory, "TempFileName"), fileContent);
        fileProviderMock.Setup(x => x.Open("TempFileName")).Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));

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
    public void PersistValidationJobAssetsCopiesStepDownloads()
    {
        var fileContent = "Some Content";
        Directory.CreateDirectory(uploadDirectory);

        File.WriteAllText(Path.Combine(uploadDirectory, "TempFileName"), fileContent);
        fileProviderMock.Setup(x => x.Open("TempFileName")).Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));

        File.WriteAllText(Path.Combine(uploadDirectory, "mylogfile"), fileContent);
        fileProviderMock.Setup(x => x.Open("mylogfile")).Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));

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
    }

    [TestMethod]
    public void PersistValidationJobAssetsFailsWithoutJobDirectory()
    {
        var jobWithDownloads = new ProcessingJob(job.Id, new List<ProcessingJobFile> { new ProcessingJobFile("OriginalName", "TempFileName") }, null, DateTime.Now)
        {
            Pipeline = BuildPipelineWithDeliveryFiles("myStep", new List<PersistedFile> { new PersistedFile("mylogfile.log", "mylogfile") }),
        };
        validationServiceMock.Setup(s => s.GetJob(job.Id)).Returns(jobWithDownloads);

        Assert.ThrowsExactly<ArgumentNullException>(() => assetHandler.PersistJobAssets(job.Id));
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
