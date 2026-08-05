using Geopilot.Pipeline.Processes.XtfErrorVisualization;

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

        Assert.AreEqual("DMAV_Gebaeudeadressen_V1_0", item.Model);
        Assert.AreEqual("Gebaeudeadressen", item.Topic);
        Assert.AreEqual("Gebaeudeeingang", item.Class);
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

        Assert.AreEqual("DMAV_Grundstuecke_V1_0", item.Model);
        Assert.AreEqual("Grundstuecke", item.Topic);
        Assert.AreEqual("Grenzpunkt", item.Class);
    }

    [TestMethod]
    public void MapLeavesClassUnsetWhenUnavailable()
    {
        var error = new LogError { Message = "value <abc> is not a number in attribute Nummer", Type = "Error" };

        var item = MapSingle(error);

        Assert.IsNull(item.Model);
        Assert.IsNull(item.Topic);
        Assert.IsNull(item.Class);
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

    [TestMethod]
    public void MapSetsTidOnlyWhenPresent()
    {
        var withTid = MapSingle(new LogError { Message = "Attribute X requires a value", Type = "Error", Tid = "tid-1" });
        Assert.AreEqual("tid-1", withTid.Tid);

        var withEmptyTid = MapSingle(new LogError { Message = "Attribute X requires a value", Type = "Error", Tid = string.Empty });
        Assert.IsNull(withEmptyTid.Tid, "an empty TID must map to null so the frontend falls back to the message");
    }

    [TestMethod]
    public void MapClassifiesErrorType()
    {
        var classified = MapSingle(new LogError { Message = "Attribute Hoehengenauigkeit requires a value", Type = "Error" });
        Assert.IsNotNull(classified.ErrorType);
        Assert.AreEqual("Mandatory attribute missing", classified.ErrorType["en"]);

        var unclassified = MapSingle(new LogError { Message = "some message no classifier knows", Type = "Error" });
        Assert.IsNull(unclassified.ErrorType);
    }

    [TestMethod]
    public void MapSetsMessageLineAndCoordinates()
    {
        var error = new LogError
        {
            Message = "Attribute X requires a value",
            Type = "Error",
            Line = 42,
            Geometry = new Geometry { Coord = new Coord { C1 = 2600000.123m, C2 = 1200000.456m } },
        };

        var item = MapSingle(error);

        Assert.AreEqual("Attribute X requires a value", item.Message);
        Assert.AreEqual(42, item.Line);
        Assert.AreEqual("2600000.123, 1200000.456", item.Coordinates);

        var minimal = MapSingle(new LogError { Message = "Attribute X requires a value", Type = "Error" });
        Assert.IsNull(minimal.Line);
        Assert.IsNull(minimal.Coordinates);
    }

    private static TreeItem MapSingle(LogError error)
    {
        var items = LogErrorToTreeItemMapper.Map(new[] { new IndexedError("e0", error) });
        Assert.HasCount(1, items);
        return items[0];
    }
}
