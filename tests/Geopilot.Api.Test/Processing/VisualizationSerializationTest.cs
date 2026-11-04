using Geopilot.Api.Processing;
using Geopilot.Pipeline.Processes.XtfDiffVisualization;
using Geopilot.Pipeline.Processes.XtfErrorVisualization;
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

        var json = JsonSerializer.Serialize(config.ToVisualization(), ProcessingRunner.VisualizationJsonOptions);

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

    [TestMethod]
    public void SerializesXtfDiffVisualizationWireFormat()
    {
        var config = new XtfDiffVisualizationConfig
        {
            Map = new MapVisualizationConfig
            {
                Layers =
                [
                    new MapLayer
                    {
                        Title = new Dictionary<string, string> { ["de"] = "Vorher" },
                        State = DiffState.Old,
                        Features = [new MapFeature { Id = "building", Geom = "POINT(2600000 1200000)", Info = "Model.Topic.Class" }],
                    },
                ],
            },
            Table = new TableVisualizationConfig
            {
                Changes =
                [
                    new DiffChange
                    {
                        Id = "c0",
                        ObjectId = "building",
                        ChangeType = DiffChangeType.Changed,
                        ValueType = DiffValueType.Attribute,
                        InterlisName = "Model.Topic.Class",
                        AttributePath = "EGID",
                        OldValue = "190001",
                        NewValue = "190002",
                    },
                    new DiffChange
                    {
                        Id = "c1",
                        ObjectId = "point",
                        ChangeType = DiffChangeType.Added,
                        ValueType = DiffValueType.Object,
                        InterlisName = "Model.Topic.Point",
                    },
                ],
            },
        };

        var json = JsonSerializer.Serialize(config.ToVisualization(), ProcessingRunner.VisualizationJsonOptions);

        // The exact wire format the frontend types mirror: camelCase properties and enum values, null fields omitted.
        var expected =
            "{\"type\":\"xtfDiff\",\"data\":{" +
            "\"map\":{\"layers\":[{\"title\":{\"de\":\"Vorher\"}," +
            "\"features\":[{\"id\":\"building\",\"geom\":\"POINT(2600000 1200000)\",\"info\":\"Model.Topic.Class\"}]," +
            "\"state\":\"old\"}]}," +
            "\"table\":{\"changes\":[" +
            "{\"id\":\"c0\",\"objectId\":\"building\",\"changeType\":\"changed\",\"valueType\":\"attribute\"," +
            "\"interlisName\":\"Model.Topic.Class\",\"attributePath\":\"EGID\",\"oldValue\":\"190001\",\"newValue\":\"190002\"}," +
            "{\"id\":\"c1\",\"objectId\":\"point\",\"changeType\":\"added\",\"valueType\":\"object\",\"interlisName\":\"Model.Topic.Point\"}]}}}";
        Assert.AreEqual(expected, json);
    }
}
