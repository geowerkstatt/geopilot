using Geopilot.PipelineCore.Pipeline;
using System.Text.Json;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class LocalizedTextTest
{
    private static readonly Dictionary<string, string> SampleValues = new()
    {
        { "de", "Hallo" },
        { "en", "Hello" },
    };

    [TestMethod]
    public void IndexerReturnsValueOrNull()
    {
        LocalizedText text = SampleValues;
        Assert.AreEqual("Hallo", text["de"]);
        Assert.IsNull(text["fr"]);
    }

    [TestMethod]
    public void TryGetReportsPresence()
    {
        LocalizedText text = SampleValues;
        Assert.IsTrue(text.TryGet("en", out var en));
        Assert.AreEqual("Hello", en);
        Assert.IsFalse(text.TryGet("it", out _));
    }

    [TestMethod]
    public void LanguagesAndCountAndIsEmpty()
    {
        LocalizedText text = SampleValues;
        Assert.AreEqual(2, text.Count);
        CollectionAssert.AreEquivalent(new[] { "de", "en" }, text.Languages.ToArray());
        Assert.IsFalse(text.IsEmpty);
        Assert.IsTrue(LocalizedText.Empty.IsEmpty);
    }

    [TestMethod]
    public void EqualityIsStructuralAndOrderIndependent()
    {
        LocalizedText a = new Dictionary<string, string> { { "de", "x" }, { "en", "y" } };
        LocalizedText b = new Dictionary<string, string> { { "en", "y" }, { "de", "x" } };
        LocalizedText c = new Dictionary<string, string> { { "de", "x" } };
        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        Assert.AreNotEqual(a, c);
    }

    [TestMethod]
    public void MapTransformsEachValue()
    {
        LocalizedText text = new Dictionary<string, string> { { "de", "{0} Datei" }, { "en", "{0} file" } };
        var mapped = text.Map(s => string.Format(System.Globalization.CultureInfo.InvariantCulture, s, 3));
        LocalizedText expected = new Dictionary<string, string> { { "de", "3 Datei" }, { "en", "3 file" } };
        Assert.AreEqual(expected, mapped);
    }

    [TestMethod]
    public void MergeJoinsSameLanguageWithSeparator()
    {
        LocalizedText first = new Dictionary<string, string> { { "de", "A" }, { "en", "A" } };
        LocalizedText second = new Dictionary<string, string> { { "de", "B" } };
        var merged = LocalizedText.Merge(new[] { first, second }, " - ");
        Assert.AreEqual("A - B", merged["de"]);
        Assert.AreEqual("A", merged["en"]);
    }

    [TestMethod]
    public void SerializesToFlatObject()
    {
        LocalizedText text = SampleValues;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(text));
        Assert.AreEqual(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.AreEqual("Hallo", doc.RootElement.GetProperty("de").GetString());
        Assert.AreEqual("Hello", doc.RootElement.GetProperty("en").GetString());
        Assert.AreEqual(2, doc.RootElement.EnumerateObject().Count());
    }

    [TestMethod]
    public void RoundTripsThroughJson()
    {
        LocalizedText text = SampleValues;
        var json = JsonSerializer.Serialize(text);
        var back = JsonSerializer.Deserialize<LocalizedText>(json);
        Assert.AreEqual(text, back);
    }

    [TestMethod]
    public void MutatingSourceDictionaryDoesNotAffectInstance()
    {
        var source = new Dictionary<string, string> { { "de", "Hallo" } };
        LocalizedText text = source;
        source["de"] = "Geändert";
        source["fr"] = "Bonjour";
        Assert.AreEqual("Hallo", text["de"]);
        Assert.IsNull(text["fr"]);
    }

    [TestMethod]
    public void ToDictionaryReturnsIndependentCopy()
    {
        LocalizedText text = new Dictionary<string, string> { { "de", "Hallo" } };
        var dict = (Dictionary<string, string>)text.ToDictionary();
        dict["de"] = "Geändert";
        Assert.AreEqual("Hallo", text["de"]);
    }
}
