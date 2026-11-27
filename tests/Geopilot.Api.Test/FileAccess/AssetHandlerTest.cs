using Geopilot.Api.Models;
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
    private Mock<IAssetFileStore> assetFileStoreMock;
    private Mock<IAssetStagingFileStore> assetStagingFileStoreMock;
    private Context context;
    private AssetHandler assetHandler;
    private Guid jobId;
    private string assetDirectory;

    [TestInitialize]
    public void Initialize()
    {
        jobId = Guid.NewGuid();
        assetDirectory = AssemblyInitialize.TestDirectoryProvider.GetAssetDirectoryPath(jobId);
        context = AssemblyInitialize.DbFixture.GetTestContext();
        loggerMock = new Mock<ILogger<AssetHandler>>();
        assetFileStoreMock = new Mock<IAssetFileStore>();
        assetStagingFileStoreMock = new Mock<IAssetStagingFileStore>();
        assetHandler = new AssetHandler(loggerMock.Object, context, assetFileStoreMock.Object, assetStagingFileStoreMock.Object, AssemblyInitialize.TestDirectoryProvider, new Mock<IContentTypeProvider>().Object);
    }

    [TestCleanup]
    public void Cleanup() => context.Dispose();

    [TestMethod]
    public async Task RecordJobAssetsDeliveryFileFromUploadRecordsAsPrimaryData()
    {
        SetupStagedFile("uploaded.xtf");
        SeedRun(new PersistedFile("uploaded.xtf", "uploaded.xtf", FromUpload: true));

        var assets = (await assetHandler.RecordJobAssetsAsync(jobId, CancellationToken.None)).ToList();

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
        SeedRun(new PersistedFile("validator_logfile.log", "logfile.log", FromUpload: false));

        var assets = (await assetHandler.RecordJobAssetsAsync(jobId, CancellationToken.None)).ToList();

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
        SeedRun(
            new PersistedFile("uploaded.xtf", "uploaded.xtf", FromUpload: true),
            new PersistedFile("report.pdf", "report.pdf", FromUpload: false));

        var assets = (await assetHandler.RecordJobAssetsAsync(jobId, CancellationToken.None)).ToList();

        Assert.AreEqual(AssetType.PrimaryData, assets.Single(a => a.OriginalFilename == "uploaded.xtf").AssetType);
        Assert.AreEqual(AssetType.ProcessedData, assets.Single(a => a.OriginalFilename == "report.pdf").AssetType);
    }

    [TestMethod]
    public async Task ManifestComesFromTheProtocol()
    {
        SetupStagedFile("uploaded.xtf");
        SetupStagedFile("report.pdf");
        var run = NewRun();
        var second = NewStep(run, order: 1);
        second.Artifacts.Add(Artifact(ArtifactKind.Delivery, new PersistedFile("report.pdf", "report.pdf", FromUpload: false)));
        second.Artifacts.Add(Artifact(ArtifactKind.Download, new PersistedFile("log.txt", "log.txt", FromUpload: false)));
        var first = NewStep(run, order: 0);
        first.Artifacts.Add(Artifact(ArtifactKind.Delivery, new PersistedFile("uploaded.xtf", "uploaded.xtf", FromUpload: true)));
        context.Recordings.Add(run);
        context.SaveChanges();

        var assets = (await assetHandler.RecordJobAssetsAsync(jobId, CancellationToken.None)).ToList();

        CollectionAssert.AreEqual(new[] { "uploaded.xtf", "report.pdf" }, assets.Select(a => a.OriginalFilename).ToArray(), "Only delivery files, in the order of their steps.");
        CollectionAssert.AreEqual(new[] { AssetType.PrimaryData, AssetType.ProcessedData }, assets.Select(a => a.AssetType).ToArray());
    }

    [TestMethod]
    public async Task RecordJobAssetsHashesTheStagedFilesWithoutMovingThem()
    {
        SetupStagedFile("report.pdf");
        SeedRun(new PersistedFile("report.pdf", "report.pdf", FromUpload: false));

        var asset = (await assetHandler.RecordJobAssetsAsync(jobId, CancellationToken.None)).Single();

        CollectionAssert.AreEquivalent(SHA256.HashData(Encoding.UTF8.GetBytes(FileContent)), asset.FileHash);
        assetFileStoreMock.Verify(x => x.PromoteStagedFiles(It.IsAny<Guid>()), Times.Never, "the delivery is not saved yet");
        assetFileStoreMock.Verify(x => x.OpenFile(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RecordJobAssetsIgnoresFilesLeftInTheDeliveryDirectory()
    {
        // Remnants of a deleted delivery are no payload: without staging the job cannot be delivered again.
        assetStagingFileStoreMock.Setup(x => x.OpenFile(jobId, "report.pdf")).Throws(new FileNotFoundException());
        assetFileStoreMock.Setup(x => x.OpenFile(jobId, "report.pdf")).Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(FileContent)));
        SeedRun(new PersistedFile("report.pdf", "report.pdf", FromUpload: false));

        await Assert.ThrowsAsync<FileNotFoundException>(() => assetHandler.RecordJobAssetsAsync(jobId, CancellationToken.None));
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
        assetHandler.DeleteJobAssets(jobId);
        Assert.IsFalse(Directory.Exists(assetDirectory));
    }

    // Writes a run with one step that holds the delivery files into the protocol only.
    private void SeedRun(params PersistedFile[] deliveryFiles)
    {
        var run = NewRun();
        var step = NewStep(run, order: 0);
        foreach (var file in deliveryFiles)
            step.Artifacts.Add(Artifact(ArtifactKind.Delivery, file));

        context.Recordings.Add(run);
        context.SaveChanges();
    }

    private Recording NewRun() => new()
    {
        JobId = jobId,
        PipelineId = "pipe_a",
        Definition = "{}",
        AppVersion = "test",
        UploadId = Guid.NewGuid(),
        UploadStorageLocation = "https://storage.example.com/uploads",
        UploadInitiatedAt = DateTime.UtcNow,
        StartedAt = DateTime.UtcNow,
    };

    private static RecordingStep NewStep(Recording run, int order)
    {
        var step = new RecordingStep
        {
            Order = order,
            StepId = $"step{order}",
            DisplayName = TestHelpers.Localized($"step{order}"),
            State = StepState.Success,
            StartedAt = DateTime.UtcNow,
        };
        run.Steps.Add(step);
        return step;
    }

    private static RecordingArtifact Artifact(ArtifactKind kind, PersistedFile file) => new()
    {
        Kind = kind,
        OriginalFileName = file.OriginalFileName,
        PersistedFileName = file.PersistedFileName,
        FromUpload = file.FromUpload,
    };

    private void SetupStagedFile(string persistedFileName)
    {
        assetStagingFileStoreMock
            .Setup(x => x.OpenFile(jobId, persistedFileName))
            .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(FileContent)));
    }
}
