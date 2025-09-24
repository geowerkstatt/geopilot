using Geopilot.Api.Models;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Geopilot.Api.FileAccess;

[TestClass]
public class AssetHandlerTest
{
    private Mock<ILogger<AssetHandler>> loggerMock;
    private Mock<IValidationService> validationServiceMock;
    private Mock<IFileProvider> fileProviderMock;
    private AssetHandler assetHandler;
    private ValidationJob job;
    private string uploadDirectory;
    private string assetDirectory;

    [TestInitialize]
    public void Initialize()
    {
        job = new ValidationJob(Guid.NewGuid(), "OriginalName", "TempFileName", ImmutableDictionary<string, ValidatorResult?>.Empty, Status.Completed);
        uploadDirectory = AssemblyInitialize.TestDirectoryProvider.GetUploadDirectoryPath(job.Id);
        assetDirectory = AssemblyInitialize.TestDirectoryProvider.GetAssetDirectoryPath(job.Id);
        loggerMock = new Mock<ILogger<AssetHandler>>();
        validationServiceMock = new Mock<IValidationService>();
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
    public void PersistValidationJobAssetsCopiesValidationJobFiles()
    {
        var fileContent = "Some Content";
        Directory.CreateDirectory(uploadDirectory);

        File.WriteAllText(Path.Combine(uploadDirectory, "TempFileName"), fileContent);
        fileProviderMock.Setup(x => x.Open("TempFileName")).Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));

        File.WriteAllText(Path.Combine(uploadDirectory, "mylogfile"), fileContent);
        fileProviderMock.Setup(x => x.Open("mylogfile")).Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));

        var logFiles = new Dictionary<string, string>
        {
            { "mylogtype", "mylogfile" },
        }.ToImmutableDictionary();
        var validatorResults = new Dictionary<string, ValidatorResult?>
        {
            { "myValidator", new ValidatorResult(ValidatorResultStatus.Completed, string.Empty, logFiles) },
        }.ToImmutableDictionary();
        var jobWithLogFiles = new ValidationJob(job.Id, "OriginalName", "TempFileName", validatorResults, Status.Completed);
        validationServiceMock.Setup(s => s.GetJob(job.Id)).Returns(jobWithLogFiles);

        var assets = assetHandler.PersistJobAssets(job.Id);

        Assert.IsTrue(File.Exists(Path.Combine(assetDirectory, "mylogfile")));
        var logfileAsset = assets.FirstOrDefault(a => a.AssetType == AssetType.ValidationReport);
        Assert.IsNotNull(logfileAsset);
        Assert.AreEqual(AssetType.ValidationReport, logfileAsset.AssetType);
        Assert.AreEqual("mylogfile", logfileAsset.SanitizedFilename);
        Assert.AreEqual("myValidator_mylogtype", logfileAsset.OriginalFilename);
        Assert.AreEqual(fileContent, File.ReadAllText(Path.Combine(assetDirectory, "mylogfile")));
        CollectionAssert.AreEquivalent(SHA256.HashData(Encoding.UTF8.GetBytes(fileContent)), logfileAsset.FileHash);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PersistValidationJobAssetsFailsWithoutJobDirectory()
    {
        var logFiles = new Dictionary<string, string>
        {
            { "mylogtype", "mylogfile" },
        }.ToImmutableDictionary();
        var validatorResults = new Dictionary<string, ValidatorResult?>
        {
            { "myValidator", new ValidatorResult(ValidatorResultStatus.Completed, string.Empty, logFiles) },
        }.ToImmutableDictionary();
        var jobWithLogFiles = new ValidationJob(job.Id, "OriginalName", "TempFileName", validatorResults, Status.Completed);
        validationServiceMock.Setup(s => s.GetJob(job.Id)).Returns(jobWithLogFiles);

        var assets = assetHandler.PersistJobAssets(job.Id);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void PersistValidationJobAssetsFailsWithoutJobFiles()
    {
        var assets = assetHandler.PersistJobAssets(Guid.NewGuid());
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
