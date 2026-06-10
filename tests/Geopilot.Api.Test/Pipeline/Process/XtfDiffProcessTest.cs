using Geopilot.Api.Pipeline.Process.XtfDiff;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using Moq;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class XtfDiffProcessTest
{
    [TestMethod]
    public async Task ThrowsWithoutTwoXtfFiles()
    {
        var process = CreateProcess(xtfDiffToolJarPath: null);
        var inputFiles = new IPipelineFile[]
        {
            new PipelineFile("TestData/XtfDiff/diff.json", "single.xtf"),
            new PipelineFile("TestData/XtfDiff/diff.json", "readme.txt"),
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(inputFiles, CancellationToken.None));
        StringAssert.Contains(exception.Message, "exactly two XTF files");
    }

    [TestMethod]
    public async Task ThrowsWithoutConfiguredJarPath()
    {
        var process = CreateProcess(xtfDiffToolJarPath: null);
        var inputFiles = new IPipelineFile[]
        {
            new PipelineFile("TestData/XtfDiff/diff.json", "a.xtf"),
            new PipelineFile("TestData/XtfDiff/diff.json", "b.xtf"),
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(inputFiles, CancellationToken.None));
        StringAssert.Contains(exception.Message, "not configured");
    }

    [TestMethod]
    public async Task ThrowsWithMissingJarFile()
    {
        var process = CreateProcess(xtfDiffToolJarPath: "does-not-exist/XTF-Diff-Tool.jar");
        var inputFiles = new IPipelineFile[]
        {
            new PipelineFile("TestData/XtfDiff/diff.json", "a.xtf"),
            new PipelineFile("TestData/XtfDiff/diff.json", "b.xtf"),
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync(inputFiles, CancellationToken.None));
        StringAssert.Contains(exception.Message, "was not found");
    }

    private static XtfDiffProcess CreateProcess(string? xtfDiffToolJarPath)
    {
        var pipelineFileManagerMock = new Mock<IPipelineFileManager>();
        pipelineFileManagerMock.Setup(m => m.GeneratePipelineFile(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string originalFileName, string fileExtension) =>
            {
                var filePath = Path.Combine(Path.GetTempPath(), $"{originalFileName}_{Guid.NewGuid()}.{fileExtension}");
                return new PipelineFile(filePath, originalFileName + "." + fileExtension);
            });

        return new XtfDiffProcess(
            javaPath: null,
            xtfDiffToolJarPath: xtfDiffToolJarPath,
            modelDirectory: null,
            baseMapWmtsCapabilitiesUrl: null,
            pipelineFileManager: pipelineFileManagerMock.Object,
            logger: Mock.Of<ILogger<XtfDiffProcessTest>>());
    }
}
