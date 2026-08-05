using Geopilot.Api.Processing;
using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Pipeline;
using System.Text.Json;

namespace Geopilot.Api.Test.Processing;

[TestClass]
public class VisualizationSerializationTest
{
    [TestMethod]
    public void SerializesXtfErrorVisualizationWireFormat()
    {
        var config = new XtfErrorVisualizationConfig
        {
            Tree = new TreeVisualizationConfig
            {
                Items =
                [
                    new TreeItem
                    {
                        Id = "e0",
                        Severity = "error",
                        ErrorType = new LocalizedText(new Dictionary<string, string> { ["de"] = "Pflichtattribut fehlt", ["en"] = "Mandatory attribute missing" }),
                        Tid = "tid-1",
                        Model = "Model_V1_0",
                        Topic = "Topic1",
                        Class = "Class1",
                        Message = "Attribute X requires a value",
                        Line = 42,
                        Coordinates = "2600000.123, 1200000.456",
                    },
                    new TreeItem
                    {
                        Severity = "warning",
                        Message = "Some warning",
                    },
                ],
                GroupBy = [TreeField.Model, TreeField.Topic, TreeField.Class],
            },
            FilterBy = [TreeField.Class, TreeField.ErrorType],
        };

        var json = JsonSerializer.Serialize(VisualizationFactory.XtfError(config), ProcessingRunner.VisualizationJsonOptions);

        // The exact wire format the frontend types mirror: camelCase properties, camelCase enum values,
        // LocalizedText as a flat language map, line as a number, null fields omitted.
        var expected =
            "{\"type\":\"xtfError\",\"data\":{" +
            "\"tree\":{\"items\":[" +
            "{\"id\":\"e0\",\"severity\":\"error\"," +
            "\"errorType\":{\"de\":\"Pflichtattribut fehlt\",\"en\":\"Mandatory attribute missing\"}," +
            "\"tid\":\"tid-1\",\"model\":\"Model_V1_0\",\"topic\":\"Topic1\",\"class\":\"Class1\"," +
            "\"message\":\"Attribute X requires a value\",\"line\":42,\"coordinates\":\"2600000.123, 1200000.456\"}," +
            "{\"severity\":\"warning\",\"message\":\"Some warning\"}]," +
            "\"groupBy\":[\"model\",\"topic\",\"class\"]}," +
            "\"filterBy\":[\"class\",\"errorType\"]}}";
        Assert.AreEqual(expected, json);
    }
}
