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
        var parameterization = new Parameterization()
            {
                { "archive_file_name", "myPersonalZipArchive" },
            };
        var process = new ZipPackageProcess();
        process.Initialize(parameterization);
        var uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var processResult = Task.Run(() => process.RunAsync(new IPipelineTransferFile[] { uploadFile })).GetAwaiter().GetResult();
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        var zipArchive = outputData as IPipelineTransferFile;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("myPersonalZipArchive.zip", zipArchive.OrginalFileName);
    }

    [TestMethod]
    public void NoArchiveFileNameProvided()
    {
        var parameterization = new Parameterization();
        var process = new ZipPackageProcess();
        process.Initialize(parameterization);
        var uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var processResult = Task.Run(() => process.RunAsync(new IPipelineTransferFile[] { uploadFile })).GetAwaiter().GetResult();
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        var zipArchive = outputData as IPipelineTransferFile;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("archive.zip", zipArchive.OrginalFileName);
    }

    [TestMethod]
    public void NoInputFilesProvided()
    {
        var parameterization = new Parameterization();
        var process = new ZipPackageProcess();
        process.Initialize(parameterization);
        var exception = Assert.Throws<ArgumentException>(() => Task.Run(() => process.RunAsync(Array.Empty<IPipelineTransferFile>())).GetAwaiter().GetResult());
        Assert.AreEqual("ZipPackageProcess: No valid input files found.", exception.Message);
    }
}
