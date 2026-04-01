using Geopilot.Api.Pipeline;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineFilesTest
{
    private PipelineFile roadsExdm2ienXtfFile = new PipelineFile("TestData/UploadFiles/RoadsExdm2ien.xtf", "RoadsExdm2ien.xtf");
    private PipelineFile helloWorldPdfFile = new PipelineFile("TestData/UploadFiles/helloWorld.pdf", "helloWorld.pdf");

    [TestMethod]
    public async Task FileExtensionFilter()
    {
        PipelineFileList pipelineFiles = new PipelineFileList(new List<IPipelineFile> { roadsExdm2ienXtfFile, helloWorldPdfFile });
        var filteredFiles = pipelineFiles.WithExtensions(new HashSet<string> { "XTF" });
        Assert.HasCount(1, filteredFiles.Files);
        Assert.AreEqual("RoadsExdm2ien.xtf", filteredFiles.Files.First().OriginalFileName);
    }

    [TestMethod]
    [DataRow("Roads*", 1)]
    [DataRow(@"^\w{1,11}\.[^.]+$", 1)]
    public async Task NameFilter(string pattern, int expectedNumber)
    {
        PipelineFileList pipelineFiles = new PipelineFileList(new List<IPipelineFile> { roadsExdm2ienXtfFile, helloWorldPdfFile });
        var filteredFiles = pipelineFiles.WithMatchingName(pattern);
        Assert.HasCount(expectedNumber, filteredFiles.Files);
    }
}
