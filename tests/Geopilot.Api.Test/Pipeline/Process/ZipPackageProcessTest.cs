using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class ZipPackageProcessTest
{
    [TestMethod]
    public void SunnyDay()
    {
        var process = new ZipPackageProcess();
        process.Initialize("myPersonalZipArchive", new Mock<ILogger<ZipPackageProcessTest>>().Object);
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
        var process = new ZipPackageProcess();
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
        var process = new ZipPackageProcess();
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync(Array.Empty<IPipelineTransferFile>()));
        Assert.AreEqual("ZipPackageProcess: No input files provided.", exception.Message);
    }

    [TestMethod]
    public async Task AllInputFilesAreNull()
    {
        var process = new ZipPackageProcess();
        var processResult = await process.RunAsync(new IPipelineTransferFile?[] { null, null, null });
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        Assert.IsNull(outputData);
    }

    [TestMethod]
    public async Task MixedNullAndValidInputFiles()
    {
        var process = new ZipPackageProcess();
        process.Initialize("mixedArchive", new Mock<ILogger<ZipPackageProcessTest>>().Object);
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
