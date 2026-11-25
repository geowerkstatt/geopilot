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
    private const string FileContent = "Some Content";

    private Mock<ILogger<AssetHandler>> loggerMock;
    private Mock<IProcessingService> validationServiceMock;
    private Mock<IAssetFileStore> assetFileStoreMock;
    private Mock<IAssetStagingFileStore> assetStagingFileStoreMock;
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
        assetFileStoreMock = new Mock<IAssetFileStore>();
        assetStagingFileStoreMock = new Mock<IAssetStagingFileStore>();
        assetHandler = new AssetHandler(loggerMock.Object, validationServiceMock.Object, assetFileStoreMock.Object, assetStagingFileStoreMock.Object, AssemblyInitialize.TestDirectoryProvider, new Mock<IContentTypeProvider>().Object);

        validationServiceMock.Setup(s => s.GetJob(job.Id)).Returns(job);
    }

    [TestMethod]
    public async Task RecordJobAssetsDeliveryFileFromUploadRecordsAsPrimaryData()
    {
        SetupStagedFile("uploaded.xtf");
        SetJobPipeline(new PersistedFile("uploaded.xtf", "uploaded.xtf", FromUpload: true));

        var assets = (await assetHandler.RecordJobAssetsAsync(job.Id, CancellationToken.None)).ToList();

        var asset = assets.Single();
        Assert.AreEqual(AssetType.PrimaryData, asset.AssetType);
        Assert.AreEqual("uploaded.xtf", asset.OriginalFilename);
        Assert.AreEqual("uploaded.xtf", asset.SanitizedFilename);
        CollectionAssert.AreEquivalent(SHA256.HashData(Encoding.UTF8.GetBytes(FileContent)), asset.FileHash);
    }

    [TestMethod]
    public async Task RecordJobAssetsDeliveryFileProducedByStepRecordsAsProcessedData()
    {
        SetupStagedFile("logfile.log");
        SetJobPipeline(new PersistedFile("validator_logfile.log", "logfile.log", FromUpload: false));

        var assets = (await assetHandler.RecordJobAssetsAsync(job.Id, CancellationToken.None)).ToList();

        var asset = assets.Single();
        Assert.AreEqual(AssetType.ProcessedData, asset.AssetType);
        Assert.AreEqual("logfile.log", asset.OriginalFilename, "A delivered file is shown under the name it is stored under.");
        Assert.AreEqual("logfile.log", asset.SanitizedFilename);
        CollectionAssert.AreEquivalent(SHA256.HashData(Encoding.UTF8.GetBytes(FileContent)), asset.FileHash);

        // Delivery files are already staged: the handler hashes them in staging and never copies.
        assetFileStoreMock.Verify(x => x.CreateFile(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        assetFileStoreMock.Verify(x => x.GetPath(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RecordJobAssetsMixedOriginsDerivesTypePerFile()
    {
        SetupStagedFile("uploaded.xtf");
        SetupStagedFile("report.pdf");
        SetJobPipeline(
            new PersistedFile("uploaded.xtf", "uploaded.xtf", FromUpload: true),
            new PersistedFile("report.pdf", "report.pdf", FromUpload: false));

        var assets = (await assetHandler.RecordJobAssetsAsync(job.Id, CancellationToken.None)).ToList();

        Assert.AreEqual(AssetType.PrimaryData, assets.Single(a => a.OriginalFilename == "uploaded.xtf").AssetType);
        Assert.AreEqual(AssetType.ProcessedData, assets.Single(a => a.OriginalFilename == "report.pdf").AssetType);
    }

    [TestMethod]
    public async Task RecordJobAssetsHashesTheStagedFilesWithoutMovingThem()
    {
        SetupStagedFile("report.pdf");
        SetJobPipeline(new PersistedFile("report.pdf", "report.pdf", FromUpload: false));

        var asset = (await assetHandler.RecordJobAssetsAsync(job.Id, CancellationToken.None)).Single();

        CollectionAssert.AreEquivalent(SHA256.HashData(Encoding.UTF8.GetBytes(FileContent)), asset.FileHash);
        assetFileStoreMock.Verify(x => x.PromoteStagedFiles(It.IsAny<Guid>()), Times.Never, "the delivery is not saved yet");
        assetFileStoreMock.Verify(x => x.OpenFile(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RecordJobAssetsIgnoresFilesLeftInTheDeliveryDirectory()
    {
        // Remnants of a deleted delivery are no payload: without staging the job cannot be delivered again.
        assetStagingFileStoreMock.Setup(x => x.OpenFile(job.Id, "report.pdf")).Throws(new FileNotFoundException());
        assetFileStoreMock.Setup(x => x.OpenFile(job.Id, "report.pdf")).Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(FileContent)));
        SetJobPipeline(new PersistedFile("report.pdf", "report.pdf", FromUpload: false));

        await Assert.ThrowsAsync<FileNotFoundException>(() => assetHandler.RecordJobAssetsAsync(job.Id, CancellationToken.None));
    }

    [TestMethod]
    public async Task RecordJobAssetsJobNotFoundThrows()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await assetHandler.RecordJobAssetsAsync(Guid.NewGuid(), CancellationToken.None));
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
        => new ProcessingJob(jobId ?? Guid.NewGuid(), Guid.NewGuid(), null, DateTime.Now);

    private void SetJobPipeline(params PersistedFile[] deliveryFiles)
    {
        var jobWithPipeline = job with { Pipeline = BuildPipelineWithDeliveryFiles("myStep", deliveryFiles.ToList()) };
        validationServiceMock.Setup(s => s.GetJob(job.Id)).Returns(jobWithPipeline);
    }

    private static IPipeline BuildPipelineWithDeliveryFiles(string stepId, List<PersistedFile> deliveryFiles)
    {
        var stepMock = new Mock<IPipelineStep>();
        stepMock.SetupGet(s => s.Id).Returns(stepId);
        stepMock.SetupGet(s => s.DeliveryFiles).Returns(deliveryFiles);

        var pipelineMock = new Mock<IPipeline>();
        pipelineMock.SetupGet(p => p.Id).Returns("myPipeline");
        pipelineMock.SetupGet(p => p.Steps).Returns(new List<IPipelineStep> { stepMock.Object });
        return pipelineMock.Object;
    }

    private void SetupStagedFile(string persistedFileName)
    {
        assetStagingFileStoreMock
            .Setup(x => x.OpenFile(job.Id, persistedFileName))
            .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(FileContent)));
    }
}
