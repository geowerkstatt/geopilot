using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Test.Ilitools;

[TestClass]
public class IlivalidatorClientTest
{
    // The tool switches to its INTERLIS 1 semantics only when the data file ends in .itf, so exactly that
    // extension has to survive the transfer, lowercased for the wrapper allowlist; everything else stays with
    // the wrapper default.
    [TestMethod]
    [DataRow("data.itf", "itf")]
    [DataRow("DATA.ITF", "itf")]
    [DataRow("data.xtf", "")]
    [DataRow("data.gml", "")]
    public void TransferFileExtensionKeepsOnlyItf(string fileName, string expected)
    {
        var file = new PipelineFile(Path.Combine("TestData", "Ilitools", fileName), fileName);

        Assert.AreEqual(expected, IlivalidatorClient.TransferFileExtension(file));
    }
}
