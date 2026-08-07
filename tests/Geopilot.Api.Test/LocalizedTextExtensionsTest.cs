using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Test;

[TestClass]
public class LocalizedTextExtensionsTest
{
    [TestMethod]
    public void GetDisplayTextPrefersEnglish()
    {
        var text = new LocalizedText(new Dictionary<string, string> { ["de"] = "Hallo", ["en"] = "Hello", ["fr"] = "Bonjour" });
        Assert.AreEqual("Hello", text.GetDisplayText());
    }

    [TestMethod]
    public void GetDisplayTextFallsBackToGermanWhenEnglishMissing()
    {
        var text = new LocalizedText(new Dictionary<string, string> { ["de"] = "Hallo", ["fr"] = "Bonjour" });
        Assert.AreEqual("Hallo", text.GetDisplayText());
    }

    [TestMethod]
    public void GetDisplayTextFollowsLanguageOrderFrenchBeforeItalian()
    {
        var text = new LocalizedText(new Dictionary<string, string> { ["it"] = "Ciao", ["fr"] = "Bonjour" });
        Assert.AreEqual("Bonjour", text.GetDisplayText());
    }

    [TestMethod]
    public void GetDisplayTextUsesAnyRemainingLanguageWhenNoPreferredPresent()
    {
        var text = new LocalizedText(new Dictionary<string, string> { ["rm"] = "Allegra" });
        Assert.AreEqual("Allegra", text.GetDisplayText());
    }

    [TestMethod]
    public void GetDisplayTextReturnsEmptyStringWhenNoLanguagePresent()
    {
        Assert.AreEqual(string.Empty, LocalizedText.Empty.GetDisplayText());
    }
}
