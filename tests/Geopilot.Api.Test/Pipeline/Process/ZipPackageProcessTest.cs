using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process.ZipPackage;
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
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess("myPersonalZipArchive", pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var uploadFile = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
        var processResult = Task.Run(() => process.RunAsync(new IPipelineFile[] { uploadFile })).GetAwaiter().GetResult();
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        var zipArchive = outputData as IPipelineFile;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("myPersonalZipArchive.zip", zipArchive.OriginalFileName);
    }

    [TestMethod]
    public void NoArchiveFileNameProvided()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess(null, pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var uploadFile = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
        var processResult = Task.Run(() => process.RunAsync(new IPipelineFile[] { uploadFile })).GetAwaiter().GetResult();
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        var zipArchive = outputData as IPipelineFile;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("archive.zip", zipArchive.OriginalFileName);
    }

    [TestMethod]
    public async Task NoInputFilesProvided()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess(null, pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync(Array.Empty<IPipelineFile>()));
        Assert.AreEqual($"ZipPackageProcess: No input files provided.", exception.Message);
    }

    [TestMethod]
    public async Task AllInputFilesAreNull()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess(null, pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var processResult = await process.RunAsync(new IPipelineFile?[] { null, null, null });
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        Assert.IsNull(outputData);
    }

    [TestMethod]
    public async Task MixedNullAndValidInputFiles()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess("mixedArchive", pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>());
        var uploadFile = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
        var processResult = await process.RunAsync(new IPipelineFile?[] { null, uploadFile, null });
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        var zipArchive = outputData as IPipelineFile;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("mixedArchive.zip", zipArchive.OriginalFileName);
    }
}
