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
        var process = new ZipPackageProcess("myPersonalZipArchive", pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>(), Guid.NewGuid());
        var uploadFile = new PipelineTransferFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
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
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess(null, pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>(), Guid.NewGuid());
        var uploadFile = new PipelineTransferFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
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
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var jobId = Guid.NewGuid();
        var process = new ZipPackageProcess(null, pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>(), jobId);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => process.RunAsync(Array.Empty<IPipelineTransferFile>()));
        Assert.AreEqual($"ZipPackageProcess: No input files provided (job: {jobId}).", exception.Message);
    }

    [TestMethod]
    public async Task AllInputFilesAreNull()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var jobId = Guid.NewGuid();
        var process = new ZipPackageProcess(null, pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>(), jobId);
        var processResult = await process.RunAsync(new IPipelineTransferFile?[] { null, null, null });
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        Assert.IsNull(outputData);
    }

    [TestMethod]
    public async Task MixedNullAndValidInputFiles()
    {
        var pipelineFileManager = new PipelineFileManager(Path.GetTempPath(), "ZipPackageProcess");
        var process = new ZipPackageProcess("mixedArchive", pipelineFileManager, Mock.Of<ILogger<ZipPackageProcessTest>>(), Guid.NewGuid());
        var uploadFile = new PipelineTransferFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
        var processResult = await process.RunAsync(new IPipelineTransferFile?[] { null, uploadFile, null });
        Assert.IsNotNull(processResult);
        Assert.HasCount(1, processResult);
        processResult.TryGetValue("zip_package", out var outputData);
        var zipArchive = outputData as IPipelineTransferFile;
        Assert.IsNotNull(zipArchive);
        Assert.AreEqual("mixedArchive.zip", zipArchive.OriginalFileName);
    }
}
