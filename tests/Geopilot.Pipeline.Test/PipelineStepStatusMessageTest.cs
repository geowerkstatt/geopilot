using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class PipelineStepStatusMessageTest
{
    [TestMethod]
    public void NormalizeAcceptsLocalizedText()
    {
        LocalizedText input = new Dictionary<string, string> { { "de", "x" } };
        var result = PipelineStep.NormalizeStatusMessage(input);
        Assert.AreEqual(input, result);
    }

    [TestMethod]
    public void NormalizeAcceptsPlainDictionaryFromLegacyPlugins()
    {
        var legacy = new Dictionary<string, string> { { "de", "x" }, { "en", "y" } };
        var result = PipelineStep.NormalizeStatusMessage(legacy);
        LocalizedText expected = legacy;
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void NormalizeReturnsNullForUnrelatedData()
    {
        Assert.IsNull(PipelineStep.NormalizeStatusMessage(42));
        Assert.IsNull(PipelineStep.NormalizeStatusMessage(null));
    }
}
