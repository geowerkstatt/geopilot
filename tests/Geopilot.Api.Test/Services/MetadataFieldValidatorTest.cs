using Geopilot.Api.Models;
using Geopilot.PipelineCore.Pipeline;

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

        Assert.HasCount(1, duplicate);
        StringAssert.Contains(duplicate[0], "option 2 <canton>");
        Assert.HasCount(1, invalid);
        StringAssert.Contains(invalid[0], "<Kanton>");
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
