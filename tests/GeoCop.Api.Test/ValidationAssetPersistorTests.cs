using GeoCop.Api.Models;
using GeoCop.Api.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace GeoCop.Api;

[TestClass]
public class ValidationAssetPersistorTests
{
    private Mock<ILogger<ValidationAssetPersistor>> loggerMock;
    private Mock<IValidationService> validationServiceMock;
    private Mock<IFileProvider> fileProviderMock;
    private IConfigurationRoot configuration;
    private ValidationAssetPersistor persistor;
    private Guid jobId;
    private string uploadDirectory;
    private string assetDirectory;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        jobId = Guid.NewGuid();
        loggerMock = new Mock<ILogger<ValidationAssetPersistor>>();
        validationServiceMock = new Mock<IValidationService>();
        fileProviderMock = new Mock<IFileProvider>();
        uploadDirectory = Path.Combine(TestContext.DeploymentDirectory, "Upload");
        assetDirectory = Path.Combine(TestContext.DeploymentDirectory, "Asset");
        configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Storage:UploadDirectory", uploadDirectory },
            { "Storage:AssetsDirectory", assetDirectory },
        }).Build();

        persistor = new ValidationAssetPersistor(loggerMock.Object, validationServiceMock.Object, fileProviderMock.Object, configuration);

        validationServiceMock.Setup(s => s.GetJob(jobId)).Returns(new ValidationJob(jobId, "OriginalName", "TempFileName"));
        validationServiceMock.Setup(s => s.GetJobStatus(jobId)).Returns(new ValidationJobStatus(jobId) { Status = Status.Completed });
        Console.WriteLine($"UploadDirectory: {uploadDirectory}");
        Console.WriteLine($"AssetDirectory: {assetDirectory}");
    }

    [TestMethod]
    public void PersistValidationJobAssetsShouldCopyPrimaryFiles()
    {
        var fileContent = "Some Content";
        Directory.CreateDirectory(Path.Combine(uploadDirectory, jobId.ToString()));
        File.WriteAllText(Path.Combine(uploadDirectory, jobId.ToString(), "TempFileName"), fileContent);
        fileProviderMock.Setup(x => x.Open("TempFileName")).Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));

        Assert.IsFalse(Directory.Exists(Path.Combine(assetDirectory, jobId.ToString())));
        var assets = persistor.PersistValidationJobAssets(jobId);

        Assert.IsNotNull(assets);
        var primaryAsset = assets.FirstOrDefault(a => a.AssetType == AssetType.PrimaryData);
        Assert.IsNotNull(primaryAsset);
        Assert.AreEqual(AssetType.PrimaryData, primaryAsset.AssetType);
        Assert.AreEqual("TempFileName", primaryAsset.SanitizedFilename);
        Assert.AreEqual("OriginalName", primaryAsset.OriginalFilename);
        Assert.AreEqual(fileContent, File.ReadAllText(Path.Combine(assetDirectory, jobId.ToString(), "TempFileName")));
        CollectionAssert.AreEquivalent(SHA256.HashData(Encoding.UTF8.GetBytes(fileContent)), primaryAsset.FileHash);
    }

    [TestMethod]
    public void PersistValidationJobAssetsCopiesValidationJobFiles()
    {
        var fileContent = "Some Content";
        Directory.CreateDirectory(Path.Combine(uploadDirectory, jobId.ToString()));

        File.WriteAllText(Path.Combine(uploadDirectory, jobId.ToString(), "TempFileName"), fileContent);
        fileProviderMock.Setup(x => x.Open("TempFileName")).Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));

        File.WriteAllText(Path.Combine(uploadDirectory, jobId.ToString(), "mylogfile"), fileContent);
        fileProviderMock.Setup(x => x.Open("mylogfile")).Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContent)));

        var validatorResult = new ValidatorResult(Status.Completed, string.Empty);
        validatorResult.LogFiles.Add("mylogtype", "mylogfile");
        var validationJobStatus = new ValidationJobStatus(jobId) { Status = Status.Completed };
        validationJobStatus.ValidatorResults.Add("myValidator", validatorResult);
        validationServiceMock.Setup(s => s.GetJobStatus(jobId)).Returns(validationJobStatus);

        var assets = persistor.PersistValidationJobAssets(jobId);

        Assert.IsTrue(File.Exists(Path.Combine(assetDirectory, jobId.ToString(), "mylogfile")));
        var logfileAsset = assets.FirstOrDefault(a => a.AssetType == AssetType.ValidationReport);
        Assert.IsNotNull(logfileAsset);
        Assert.AreEqual(AssetType.ValidationReport, logfileAsset.AssetType);
        Assert.AreEqual("mylogfile", logfileAsset.SanitizedFilename);
        Assert.AreEqual("myValidator_mylogtype", logfileAsset.OriginalFilename);
        Assert.AreEqual(fileContent, File.ReadAllText(Path.Combine(assetDirectory, jobId.ToString(), "mylogfile")));
        CollectionAssert.AreEquivalent(SHA256.HashData(Encoding.UTF8.GetBytes(fileContent)), logfileAsset.FileHash);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void PersistValidationJobAssetsFailsWithoutJobDirectory()
    {
        var validatorResult = new ValidatorResult(Status.Completed, string.Empty);
        validatorResult.LogFiles.Add("mylogtype", "mylogfile");
        var validationJobStatus = new ValidationJobStatus(jobId) { Status = Status.Completed };
        validationJobStatus.ValidatorResults.Add("myValidator", validatorResult);
        validationServiceMock.Setup(s => s.GetJobStatus(jobId)).Returns(validationJobStatus);

        var assets = persistor.PersistValidationJobAssets(jobId);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void PersistValidationJobAssetsFailsWithoutJobFiles()
    {
        var assets = persistor.PersistValidationJobAssets(Guid.NewGuid());
    }
}
