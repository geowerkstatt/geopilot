using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class ZipPackageProcessTest
{
    [TestMethod]
    public void SunnyDay()
    {
        var dataHandlingConfig = new DataHandlingConfig()
        {
            OutputMapping = new Dictionary<string, string>()
                {
                    { "zip_package", "package" },
                },
        };
        var parameterization = new Parameterization()
            {
                { "archive_file_name", "myPersonalZipArchive" },
            };
        var process = new ZipPackageProcess();
        process.Initialize(dataHandlingConfig, parameterization);
        var uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var processResult = Task.Run(() => process.RunAsync(new IPipelineTransferFile[] { uploadFile })).GetAwaiter().GetResult();
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult.Data);
        processResult.Data.TryGetValue("package", out var outputData);
        var zipArchive = outputData?.Data as IPipelineTransferFile;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("myPersonalZipArchive.zip", zipArchive.OrginalFileName);
    }

    [TestMethod]
    public void NoArchiveFileNameProvided()
    {
        var dataHandlingConfig = new DataHandlingConfig()
        {
            OutputMapping = new Dictionary<string, string>()
                {
                    { "zip_package", "package" },
                },
        };
        var parameterization = new Parameterization();
        var process = new ZipPackageProcess();
        process.Initialize(dataHandlingConfig, parameterization);
        var uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var processResult = Task.Run(() => process.RunAsync(new IPipelineTransferFile[] { uploadFile })).GetAwaiter().GetResult();
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult.Data);
        processResult.Data.TryGetValue("package", out var outputData);
        var zipArchive = outputData?.Data as IPipelineTransferFile;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("archive.zip", zipArchive.OrginalFileName);
    }

    [TestMethod]
    public void NoInputFilesProvided()
    {
        var dataHandlingConfig = new DataHandlingConfig()
        {
            OutputMapping = new Dictionary<string, string>()
                {
                    { "zip_package", "package" },
                },
        };
        var parameterization = new Parameterization();
        var process = new ZipPackageProcess();
        process.Initialize(dataHandlingConfig, parameterization);
        var exception = Assert.Throws<ArgumentException>(() => Task.Run(() => process.RunAsync(Array.Empty<IPipelineTransferFile>())).GetAwaiter().GetResult());
        Assert.AreEqual("ZipPackageProcess: No valid input files found.", exception.Message);
    }

    [TestMethod]
    public void NoOutputDataHandlingProvided()
    {
        var dataHandlingConfig = new DataHandlingConfig()
        {
            OutputMapping = new Dictionary<string, string>(),
        };
        var parameterization = new Parameterization();
        var process = new ZipPackageProcess();
        process.Initialize(dataHandlingConfig, parameterization);
        var uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var exception = Assert.Throws<KeyNotFoundException>(() => Task.Run(() => process.RunAsync(new IPipelineTransferFile[] { uploadFile })).GetAwaiter().GetResult());
        Assert.AreEqual("output mapping for key 'zip_package' not found.", exception.Message);
    }

    [TestMethod]
    public void UninitializedOutputDataHandling()
    {
        var dataHandlingConfig = new DataHandlingConfig();
        var parameterization = new Parameterization();
        var process = new ZipPackageProcess();
        process.Initialize(dataHandlingConfig, parameterization);
        var uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var exception = Assert.Throws<KeyNotFoundException>(() => Task.Run(() => process.RunAsync(new IPipelineTransferFile[] { uploadFile })).GetAwaiter().GetResult());
        Assert.AreEqual("output mapping for key 'zip_package' not found.", exception.Message);
    }

    [TestMethod]
    public void UninitializedDataHandlingHandling()
    {
        var parameterization = new Parameterization();
        var process = new ZipPackageProcess();
        #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        process.Initialize(null, parameterization);
        #pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        var uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var exception = Assert.Throws<ArgumentException>(() => Task.Run(() => process.RunAsync(new IPipelineTransferFile[] { uploadFile })).GetAwaiter().GetResult());
        Assert.AreEqual("ZipPackageProcess: dataHandlingConfig is null. Cannot add output data for mapping 'zip_package'.", exception.Message);
    }
}
