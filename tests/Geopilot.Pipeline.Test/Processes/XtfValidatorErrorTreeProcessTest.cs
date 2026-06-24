using Geopilot.Pipeline;
using Geopilot.Pipeline.Processes.XtfValidatorErrorTree;
using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Pipeline;
using Newtonsoft.Json;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class XtfValidatorErrorTreeProcessTest
{
    [TestMethod]
    public async Task SunnyDay()
    {
        var process = new XtfValidatorErrorTreeProcess();
        var uploadFile = new PipelineFile("TestData/DownloadFiles/ilicop/errorLogWithErrors.xtf", "errorLogWithErrors.xtf");

        var processResult = await process.RunAsync(uploadFile).ConfigureAwait(false);
        Assert.IsNotNull(processResult);
        Assert.HasCount(2, processResult);

        var visualization = processResult["visualization"] as Visualization<TreeVisualizationConfig>;
        var statusMessage = processResult["status_message"] as Dictionary<string, string>;
        Assert.IsNotNull(visualization);

        // The envelope carries the discriminator; the frontend selects the tree component from it.
        Assert.AreEqual("tree", visualization.Type);

        var errorLog = visualization.Data.Nodes;
        Assert.IsNotEmpty(errorLog);

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

        CollectionAssert.AreEqual(expected, errorLog.ToList(), "error tree is not as expected");

        Assert.IsTrue(errorLog.All(group => group.Metadata is null), "group nodes carry no metadata");

        var uniqueConstraintGroup = errorLog.Single(group => group.Message == "Unique constraint violated");
        var occurrence = uniqueConstraintGroup.Values.Single();
        Assert.IsNotNull(occurrence.Metadata, "occurrence leaves carry metadata");
        Assert.AreEqual(occurrence.Message, occurrence.Metadata["TID"], "metadata carries the object TID");
        Assert.IsFalse(occurrence.Metadata.ContainsKey("Data source"), "metadata carries no data source");
        Assert.AreEqual("DMAV_Einzelobjekte_V1_0", occurrence.Metadata["Model"]);
        Assert.AreEqual("Einzelobjekte", occurrence.Metadata["Topic"]);
        Assert.AreEqual("EONachfuehrung", occurrence.Metadata["Class"]);
        StringAssert.StartsWith(occurrence.Metadata["Message"], "Unique constraint");
        Assert.AreEqual("54e42e10-8383-48d3-8391-70baf572f21a", occurrence.Message, "occurrence is displayed by its object TID");
    }

    private List<TreeNode>? Deserialize(string json)
    {
        using var stringReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(stringReader);
        var serializer = new JsonSerializer();
        return serializer.Deserialize<List<TreeNode>>(jsonReader);
    }
}
