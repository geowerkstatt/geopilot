using Geopilot.Api.Models;
using Geopilot.PipelineCore.Pipeline;
using System.Text.Json;

namespace Geopilot.Api.Services;

[TestClass]
public class MetadataFieldValidatorTest
{
    [TestMethod]
    public void AcceptsAFieldOfEveryType()
    {
        var fields = new List<MetadataField>
        {
            Field("text_field", MetadataFieldType.Text),
            Field("text_list", MetadataFieldType.TextList),
            Field("selection", MetadataFieldType.Selection, options: [Option("canton"), Option("municipality")]),
            Field("whole_number", MetadataFieldType.WholeNumber),
            Field("decimal_number", MetadataFieldType.DecimalNumber),
            Field("date", MetadataFieldType.Date, FieldEvaluationType.Required),
        };

        Assert.IsEmpty(MetadataFieldValidator.ValidateDefinitions(fields));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Project")]
    [DataRow("1abc")]
    [DataRow("1e3")]
    [DataRow("_x")]
    [DataRow("project-number")]
    [DataRow("constructor")]
    [DataRow("prototype")]
    public void RefusesAnIdOutsideThePattern(string id)
    {
        var problems = MetadataFieldValidator.ValidateDefinitions([Field(id)]);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], $"<{id}>");
    }

    [TestMethod]
    public void RefusesAnIdOfMoreThan64Characters()
    {
        var id = new string('a', 65);

        var problems = MetadataFieldValidator.ValidateDefinitions([Field(id)]);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], $"<{id}>");
    }

    [TestMethod]
    [DataRow("a")]
    [DataRow("project_number_2")]
    [DataRow("constructor_id")]
    public void AcceptsAnIdInThePattern(string id)
    {
        Assert.IsEmpty(MetadataFieldValidator.ValidateDefinitions([Field(id)]));
    }

    [TestMethod]
    public void AcceptsAnIdOf64Characters()
    {
        Assert.IsEmpty(MetadataFieldValidator.ValidateDefinitions([Field(new string('a', 64))]));
    }

    [TestMethod]
    public void RefusesADuplicateId()
    {
        var problems = MetadataFieldValidator.ValidateDefinitions([Field("project", title: "Projekt"), Field("project", title: "Nummer")]);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "field 2 <project>");
    }

    [TestMethod]
    public void RefusesATitleWithoutText()
    {
        var empty = new LocalizedText(new Dictionary<string, string>());
        var blank = new LocalizedText(new Dictionary<string, string> { ["de"] = " " });

        Assert.HasCount(1, MetadataFieldValidator.ValidateDefinitions([Field("a", localizedTitle: empty)]));
        Assert.HasCount(1, MetadataFieldValidator.ValidateDefinitions([Field("a", localizedTitle: blank)]));
    }

    [TestMethod]
    public void RefusesAnEvaluationOtherThanOptionalOrRequired()
    {
        var problems = MetadataFieldValidator.ValidateDefinitions([Field("a", evaluation: FieldEvaluationType.NotEvaluated)]);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "<a>");
    }

    [TestMethod]
    public void RefusesASelectionWithoutOptions()
    {
        Assert.HasCount(1, MetadataFieldValidator.ValidateDefinitions([Field("a", MetadataFieldType.Selection, options: null)]));
        Assert.HasCount(1, MetadataFieldValidator.ValidateDefinitions([Field("a", MetadataFieldType.Selection, options: [])]));
    }

    [TestMethod]
    public void RefusesOptionsOnAnotherType()
    {
        var problems = MetadataFieldValidator.ValidateDefinitions([Field("a", MetadataFieldType.Text, options: [Option("canton")])]);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "<a>");
    }

    [TestMethod]
    public void AcceptsAnEmptyOptionListOnAnotherType()
    {
        Assert.IsEmpty(MetadataFieldValidator.ValidateDefinitions([Field("a", MetadataFieldType.Text, options: [])]));
    }

    [TestMethod]
    public void RefusesInvalidOrDuplicateOptionIds()
    {
        var duplicate = MetadataFieldValidator.ValidateDefinitions([Field("a", MetadataFieldType.Selection, options: [Option("canton"), Option("canton")])]);
        var invalid = MetadataFieldValidator.ValidateDefinitions([Field("a", MetadataFieldType.Selection, options: [Option("Kanton")])]);
        var reserved = MetadataFieldValidator.ValidateDefinitions([Field("a", MetadataFieldType.Selection, options: [Option("prototype")])]);

        Assert.HasCount(1, duplicate);
        StringAssert.Contains(duplicate[0], "option 2 <canton>");
        Assert.HasCount(1, invalid);
        StringAssert.Contains(invalid[0], "<Kanton>");
        Assert.HasCount(1, reserved);
        StringAssert.Contains(reserved[0], "<prototype>");
    }

    [TestMethod]
    public void RefusesAnOptionTitleWithoutText()
    {
        var option = new MetadataFieldOption("canton", new LocalizedText(new Dictionary<string, string> { ["de"] = " " }));

        var problems = MetadataFieldValidator.ValidateDefinitions([Field("a", MetadataFieldType.Selection, options: [option])]);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "<canton>");
    }

    [TestMethod]
    public void RefusesTwoFieldsWithTheSameDisplayTitle()
    {
        var german = new LocalizedText(new Dictionary<string, string> { ["de"] = "Projekt" });
        var english = new LocalizedText(new Dictionary<string, string> { ["en"] = "Projekt" });

        var problems = MetadataFieldValidator.ValidateDefinitions([Field("a", localizedTitle: german), Field("b", localizedTitle: english)]);

        Assert.HasCount(1, problems);
        StringAssert.Contains(problems[0], "field 2 <b>");
    }

    [TestMethod]
    public void AcceptsFieldsWhoseDisplayTitlesDiffer()
    {
        var both = new LocalizedText(new Dictionary<string, string> { ["de"] = "Projekt", ["en"] = "Project" });
        var germanOnly = new LocalizedText(new Dictionary<string, string> { ["de"] = "Projekt" });

        Assert.IsEmpty(MetadataFieldValidator.ValidateDefinitions([Field("a", localizedTitle: both), Field("b", localizedTitle: germanOnly)]));
    }

    [TestMethod]
    public void StoresEveryValueInItsNormalizedForm()
    {
        var values = Values("""{ "project_number": " 2026-117 ", "municipalities": [" Aarau ", "", "Buchs"], "responsible": "canton", "parcel_count": 12, "area": 12.5, "due_date": "2026-11-04" }""");

        var check = MetadataFieldValidator.CheckValues(ValueFields(), values);

        Assert.IsEmpty(check.Errors);
        CollectionAssert.AreEqual(
            new[] { "\"2026-117\"", "[\"Aarau\",\"Buchs\"]", "\"canton\"", "12", "12.5", "\"2026-11-04\"" },
            check.Snapshot.Select(field => field.Value?.GetRawText()).ToArray());
        Assert.HasCount(2, check.Snapshot[2].Options ?? []);
    }

    [TestMethod]
    [DataRow("project_number", "12")]
    [DataRow("municipalities", "\"Aarau, Buchs\"")]
    [DataRow("municipalities", "[\"Aarau\", 1]")]
    [DataRow("responsible", "\"Kanton\"")]
    [DataRow("parcel_count", "12.5")]
    [DataRow("parcel_count", "\"12\"")]
    [DataRow("parcel_count", "12.0")]
    [DataRow("area", "\"12.5\"")]
    [DataRow("due_date", "\"04.11.2026\"")]
    [DataRow("due_date", "\"2026-02-30\"")]
    [DataRow("due_date", "\"2026-11-04T10:00\"")]
    public void RefusesAValueOfTheWrongType(string id, string json)
    {
        var values = Values("""{ "project_number": "x", "responsible": "canton" }""");
        values[id] = JsonDocument.Parse(json).RootElement.Clone();

        var check = MetadataFieldValidator.CheckValues(ValueFields(), values);

        CollectionAssert.AreEqual(new[] { $"metadata.{id}" }, check.Errors.Keys.ToArray());
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("\"\"")]
    [DataRow("\"  \"")]
    [DataRow("[]")]
    [DataRow("[\"\", \" \"]")]
    public void TreatsBlankValuesAsNotFilledIn(string json)
    {
        var values = Values("""{ "responsible": "canton" }""");
        values["municipalities"] = JsonDocument.Parse(json).RootElement.Clone();
        values["project_number"] = JsonDocument.Parse(json).RootElement.Clone();

        var check = MetadataFieldValidator.CheckValues(ValueFields(), values);

        CollectionAssert.AreEqual(new[] { "metadata.project_number" }, check.Errors.Keys.ToArray());
        Assert.IsNull(check.Snapshot[1].Value);
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("[\"\", \" \"]")]
    public void TreatsABlankArrayAsNotFilledInForEveryType(string json)
    {
        var values = Values("""{ "project_number": "x", "responsible": "canton" }""");
        values["parcel_count"] = JsonDocument.Parse(json).RootElement.Clone();
        values["due_date"] = JsonDocument.Parse(json).RootElement.Clone();
        values["area"] = JsonDocument.Parse(json).RootElement.Clone();

        var check = MetadataFieldValidator.CheckValues(ValueFields(), values);

        Assert.IsEmpty(check.Errors);
        Assert.IsNull(check.Snapshot[3].Value);
        Assert.IsNull(check.Snapshot[4].Value);
        Assert.IsNull(check.Snapshot[5].Value);
    }

    [TestMethod]
    public void RefusesAListWithAnEmptyStringAndANumber()
    {
        var values = Values("""{ "project_number": "x", "responsible": "canton", "municipalities": ["", 1] }""");

        var check = MetadataFieldValidator.CheckValues(ValueFields(), values);

        CollectionAssert.AreEqual(new[] { "metadata.municipalities" }, check.Errors.Keys.ToArray());
    }

    [TestMethod]
    public void RefusesAnUnknownId()
    {
        var values = Values("""{ "project_number": "x", "responsible": "canton", "colour": "red" }""");

        var check = MetadataFieldValidator.CheckValues(ValueFields(), values);

        CollectionAssert.AreEqual(new[] { "metadata.colour" }, check.Errors.Keys.ToArray());
    }

    [TestMethod]
    public void AcceptsNoValuesWithoutFields()
    {
        var check = MetadataFieldValidator.CheckValues([], null);

        Assert.IsEmpty(check.Errors);
        Assert.IsEmpty(check.Snapshot);
    }

    private static Dictionary<string, JsonElement> Values(string json)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? throw new AssertFailedException("The values of a test are a JSON object.");

    private static List<MetadataField> ValueFields() =>
    [
        Field("project_number", MetadataFieldType.Text, FieldEvaluationType.Required),
        Field("municipalities", MetadataFieldType.TextList),
        Field("responsible", MetadataFieldType.Selection, FieldEvaluationType.Required, options: [Option("canton"), Option("municipality")]),
        Field("parcel_count", MetadataFieldType.WholeNumber),
        Field("area", MetadataFieldType.DecimalNumber),
        Field("due_date", MetadataFieldType.Date),
    ];

    private static MetadataField Field(
        string id,
        MetadataFieldType type = MetadataFieldType.Text,
        FieldEvaluationType evaluation = FieldEvaluationType.Optional,
        string? title = null,
        LocalizedText? localizedTitle = null,
        IReadOnlyList<MetadataFieldOption>? options = null)
        => new(id, localizedTitle ?? TestHelpers.Localized(title ?? $"Title of {id}"), type, evaluation, options);

    private static MetadataFieldOption Option(string id) => new(id, TestHelpers.Localized($"Option {id}"));
}
