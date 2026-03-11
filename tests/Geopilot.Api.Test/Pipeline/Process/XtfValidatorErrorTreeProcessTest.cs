using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;
using Newtonsoft.Json;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class XtfValidatorErrorTreeProcessTest
{
    [TestMethod]
    public async Task SunnyDay()
    {
        var process = new XtfValidatorErrorTreeProcess();

        var uploadFile = new PipelineTransferFile("ErrorLogWithErrors", "TestData/DownloadFiles/ilicop/errorLogWithErrors.xtf");
        var processResult = await process.RunAsync(uploadFile).ConfigureAwait(false);
        Assert.IsNotNull(processResult);

        var errorLog = processResult["error_tree"] as List<ErrorTree>;
        var jsonErrorLog = processResult["json_error_tree"] as string;
        var jsonErrorLogFile = processResult["json_error_tree_file"] as PipelineTransferFile;

        Assert.IsNotEmpty(errorLog);
        Assert.IsNotEmpty(jsonErrorLog);
        Assert.IsNotNull(jsonErrorLogFile);

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
