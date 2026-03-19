using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;
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

        var errorLog = processResult["error_tree"] as List<ErrorTree>;
        var jsonErrorLog = processResult["json_error_tree"] as string;
        var jsonErrorLogFile = processResult["json_error_tree_file"] as PipelineFile;
        var statusMessage = processResult["status_message"] as Dictionary<string, string>;

        Assert.IsNotEmpty(errorLog);
        Assert.IsNotEmpty(jsonErrorLog);
        Assert.IsNotNull(jsonErrorLogFile);

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
    }

    private List<ErrorTree>? Deserialize(string json)
    {
        using var stringReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(stringReader);
        var serializer = new JsonSerializer();
        return serializer.Deserialize<List<ErrorTree>>(jsonReader);
    }
}
