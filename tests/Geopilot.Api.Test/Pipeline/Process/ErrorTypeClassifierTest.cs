using Geopilot.Api.Pipeline.Process.XtfValidatorErrorTree;

namespace Geopilot.Api.Test.Pipeline.Process;

[TestClass]
public class ErrorTypeClassifierTest
{
    [TestMethod]
    [DataRow("Mandatory Constraint Model.Topic.Class.C1 is not true.", "Mandatory constraint not true")]
    [DataRow("Plausibility Constraint Model.Topic.Class.C1 is not true.", "Plausibility constraint not true")]
    [DataRow("Set Constraint Model.Topic.Class.C1 is not true.", "Set constraint not true")]
    [DataRow("Unique constraint Model.Topic.Class.C1 is violated! Values 1, 2 already exist in Object: abc", "Unique constraint violated")]
    [DataRow("Existence constraint Model.Topic.Class.C1 is violated! The value of the attribute a of b was not found in the condition class.", "Existence constraint violated")]
    [DataRow("value <abc> is not a number in attribute Nummer", "Value is not a number")]
    [DataRow("value 5 is out of range in attribute Nummer", "Numeric value out of range")]
    [DataRow("value <abc> is not in range in attribute Nummer", "Value not in range")]
    [DataRow("value Bodenbedeckungen is not a member of the enumeration in attribute ImModul", "Value not a member of enumeration")]
    [DataRow("value <abc> is not a BOOLEAN in attribute Flag", "Value not a BOOLEAN")]
    [DataRow("value <abc> is not a valid UUID in attribute Id", "Value not a valid UUID")]
    [DataRow("value <abc> is not a valid OID in attribute Id", "Value not a valid OID")]
    [DataRow("value <abc> is not a valid Date in attribute Datum", "Value not a valid date")]
    [DataRow("value <abc> is a keyword in attribute Name", "Value is a reserved keyword")]
    [DataRow("invalid format of date value <abc> in attribute Datum", "Invalid date format")]
    [DataRow("invalid format of time value <abc> in attribute Zeit", "Invalid time format")]
    [DataRow("invalid format of datetime value <abc> in attribute Zeitstempel", "Invalid datetime format")]
    [DataRow("date value <abc> is not in range in attribute Datum", "Date value out of range")]
    [DataRow("time value <abc> is not in range in attribute Zeit", "Time value out of range")]
    [DataRow("datetime value <abc> is not in range in attribute Zeitstempel", "Datetime value out of range")]
    [DataRow("invalid format of INTERLIS.NAME value <abc> in attribute Name", "Invalid INTERLIS.NAME format")]
    [DataRow("invalid format of INTERLIS.URI value <abc> in attribute Uri", "Invalid INTERLIS.URI format")]
    [DataRow("does not satisfy the domain constraint D1", "Domain constraint not satisfied")]
    [DataRow("Attribute <a> has a invalid value <b>", "Invalid formatted value")]
    [DataRow("Value <a> is a out of range in attribute <b>", "Formatted value out of range")]
    [DataRow("The value <abc> is not a Polyline in attribute Geometrie", "Value is not a polyline")]
    [DataRow("The value <abc> is not a Polygon in attribute Geometrie", "Value is not a polygon")]
    [DataRow("The value <abc> is not a Coord in attribute Geometrie", "Value is not a coordinate")]
    [DataRow("unknown class <X> in attribute Ref", "Unknown class in attribute")]
    [DataRow("Attribute Hoehengenauigkeit requires a value", "Mandatory attribute missing")]
    [DataRow("Attribute Nummer has wrong number of values", "Wrong number of values")]
    [DataRow("Attribute Text is length restricted to 10", "Text too long")]
    [DataRow("Attribute Text must not contain control characters", "Control characters in text")]
    [DataRow("Attribute Geo requires a structure S", "Missing required structure")]
    [DataRow("Attribute Geo requires a non-abstract structure", "Missing required structure")]
    [DataRow("Attribute Geo has an unexpected type T", "Unexpected attribute type")]
    [DataRow("Wrong COORD structure, C1 expected", "Invalid COORD structure")]
    [DataRow("Not a type of COORD", "Invalid COORD structure")]
    [DataRow("Wrong ARC structure, C3 expected", "Invalid ARC structure")]
    [DataRow("invalid number of segments in POLYLINE", "Invalid polyline geometry")]
    [DataRow("invalid number of surfaces in COMPLETE basket", "Invalid surface geometry")]
    [DataRow("No object found with OID 123.", "Referenced object not found")]
    [DataRow("wrong class A of target object B for role R.", "Wrong target class for reference")]
    [DataRow("Model.Topic.Class should associate A to 2 target objects (instead of 0)", "Wrong association multiplicity")]
    public void ClassifyReturnsCategoryForKnownMessage(string message, string expectedCategory)
    {
        Assert.AreEqual(expectedCategory, ErrorTypeClassifier.Classify(message));
    }

    [TestMethod]
    public void ClassifyReturnsNullForUnknownMessage()
    {
        Assert.IsNull(ErrorTypeClassifier.Classify("basket DMAV.Grundstuecke is mandatory in transfer"));
    }
}
