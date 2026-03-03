using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Geopilot.PipelineCore.Pipeline;

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
        Assert.AreEqual("myPersonalZipArchive.zip", zipArchive.OriginalFileName);
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
        Assert.AreEqual("archive.zip", zipArchive.OriginalFileName);
    }

    [TestMethod]
    public async Task NoInputFilesProvided()
    {
        var parameterization = new Parameterization();
        var process = new ZipPackageProcess();
        process.Initialize(parameterization);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync(Array.Empty<IPipelineTransferFile>()));
        Assert.AreEqual("ZipPackageProcess: No input files provided.", exception.Message);
    }

    [TestMethod]
    public async Task AllInputFilesAreNull()
    {
        var parameterization = new Parameterization();
        var process = new ZipPackageProcess();
        process.Initialize(parameterization);
        var processResult = await process.RunAsync(new IPipelineTransferFile?[] { null, null, null });
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        Assert.IsNull(outputData);
    }

    [TestMethod]
    public async Task MixedNullAndValidInputFiles()
    {
        var parameterization = new Parameterization()
            {
                { "archive_file_name", "mixedArchive" },
            };
        var process = new ZipPackageProcess();
        process.Initialize(parameterization);
        var uploadFile = new PipelineTransferFile("RoadsExdm2ien", "TestData/UploadFiles/RoadsExdm2ien.xtf");
        var processResult = await process.RunAsync(new IPipelineTransferFile?[] { null, uploadFile, null });
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        var zipArchive = outputData as IPipelineTransferFile;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("mixedArchive.zip", zipArchive.OriginalFileName);
    }
}
