using Geopilot.Pipeline.Processes.XtfErrorVisualization;
using Geopilot.Pipeline.Visualization;

namespace Geopilot.Pipeline.Test.Processes;

[TestClass]
public class LogErrorToTreeItemMapperTest
{
    [TestMethod]
    public void MapTakesModelTopicClassFromObjectTag()
    {
        var error = new LogError
        {
            Message = "Attribute InAenderung requires a value",
            Type = "Error",
            Tid = "tid-1",
            ObjTag = "DMAV_Gebaeudeadressen_V1_0.Gebaeudeadressen.Gebaeudeeingang",
        };

        var item = MapSingle(error);

        Assert.AreEqual("DMAV_Gebaeudeadressen_V1_0", item.Metadata["Model"]);
        Assert.AreEqual("Gebaeudeadressen", item.Metadata["Topic"]);
        Assert.AreEqual("Gebaeudeeingang", item.Metadata["Class"]);
    }

    [TestMethod]
    public void MapRecoversClassFromMessageWhenObjectTagIsEmpty()
    {
        var error = new LogError
        {
            Message = "Mandatory Constraint DMAV_Grundstuecke_V1_0.Grundstuecke.Grenzpunkt.C1 is not true.",
            Type = "Error",
        };

        var item = MapSingle(error);

        Assert.AreEqual("DMAV_Grundstuecke_V1_0", item.Metadata["Model"]);
        Assert.AreEqual("Grundstuecke", item.Metadata["Topic"]);
        Assert.AreEqual("Grenzpunkt", item.Metadata["Class"]);
    }

    [TestMethod]
    public void MapLeavesClassUnsetWhenUnavailable()
    {
        var error = new LogError { Message = "value <abc> is not a number in attribute Nummer", Type = "Error" };

        var item = MapSingle(error);

        Assert.IsFalse(item.Metadata.ContainsKey("Model"));
        Assert.IsFalse(item.Metadata.ContainsKey("Class"));
    }

    [TestMethod]
    public void MapSkipsInfoEntriesAndEmptyMessages()
    {
        var entries = new[]
        {
            new IndexedError("e0", new LogError { Message = "informational", Type = "Info" }),
            new IndexedError("e1", new LogError { Message = null, Type = "Error" }),
            new IndexedError("e2", new LogError { Message = "Attribute X requires a value", Type = "Warning" }),
        };

        var items = LogErrorToTreeItemMapper.Map(entries);

        Assert.HasCount(1, items);
        Assert.AreEqual("warning", items[0].Severity);
    }

    private static TreeItem MapSingle(LogError error)
    {
        var items = LogErrorToTreeItemMapper.Map(new[] { new IndexedError("e0", error) });
        Assert.HasCount(1, items);
        return items[0];
    }
}
