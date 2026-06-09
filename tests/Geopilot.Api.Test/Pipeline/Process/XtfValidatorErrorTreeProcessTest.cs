using Geopilot.Api.Pipeline.Process.TreeVisualization;
using Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Moq;
using Newtonsoft.Json;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class XtfValidatorErrorTreeProcessTest
{
    [TestMethod]
    public async Task SunnyDay()
    {
        var pipelineFileManagerMock = new Mock<IPipelineFileManager>();

        pipelineFileManagerMock.Setup(m => m.GeneratePipelineFile(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string originalFileName, string fileExtension) =>
            {
                var filePath = Path.Combine(Path.GetTempPath(), $"{originalFileName}_{Guid.NewGuid()}.{fileExtension}");
                return new PipelineFile(filePath, originalFileName + "." + fileExtension);
            });
        var process = new XtfValidatorErrorTreeProcess(pipelineFileManagerMock.Object);

        var uploadFile = new PipelineFile("TestData/DownloadFiles/ilicop/errorLogWithErrors.xtf", "errorLogWithErrors.xtf");
        var processResult = await process.RunAsync(uploadFile).ConfigureAwait(false);
        Assert.IsNotNull(processResult);
        Assert.HasCount(4, processResult);

        var errorLog = processResult["error_tree"] as List<TreeNode>;
        var jsonErrorLog = processResult["json_error_tree"] as string;
        var treeConfigFile = processResult["tree_config"] as PipelineFile;
        var statusMessage = processResult["status_message"] as Dictionary<string, string>;

        Assert.IsNotEmpty(errorLog);
        Assert.IsNotEmpty(jsonErrorLog);
        Assert.IsNotNull(treeConfigFile);

        Assert.HasCount(4, statusMessage);
        var expectedStatusMessage = new Dictionary<string, string>()
        {
            { "de", "Error Tree erstellt" },
            { "fr", "Arbre d'erreurs créé" },
            { "it", "Albero degli errori creato" },
            { "en", "Error tree created" },
        };
        CollectionAssert.AreEqual(expectedStatusMessage, statusMessage);

        var expected = Deserialize(File.ReadAllText("TestData/Expectations/XtfValidatorErrorTree/errorLogWithErrors.json"));

        CollectionAssert.AreEqual(expected, errorLog, "error tree is not as expected");

        var errorLeafWithMetadata = errorLog[0].Values[0].Values.Last().Values[0];
        Assert.IsNotNull(errorLeafWithMetadata.Metadata, "error leaves carry metadata");
        Assert.AreEqual("19088", errorLeafWithMetadata.Metadata["Line"]);
        Assert.IsNull(errorLog[0].Metadata, "grouping nodes carry no metadata");
    }

    private List<TreeNode>? Deserialize(string json)
    {
        using var stringReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(stringReader);
        var serializer = new JsonSerializer();
        return serializer.Deserialize<List<TreeNode>>(jsonReader);
    }
}
