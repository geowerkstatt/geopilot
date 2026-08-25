using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Geowerkstatt.IlitoolsWrapperApi.Ilivalidator;

namespace Geopilot.Pipeline.Test.Ilitools;

[TestClass]
public class IlivalidatorClientTest
{
    // The tool switches to its INTERLIS 1 semantics only when the data file ends in .itf, so exactly that
    // extension has to select the ITF transfer file type; everything else is sent as XTF.
    [TestMethod]
    [DataRow("data.itf", IlivalidatorFileType.TransferFileItf)]
    [DataRow("DATA.ITF", IlivalidatorFileType.TransferFileItf)]
    [DataRow("data.xtf", IlivalidatorFileType.TransferFileXtf)]
    [DataRow("data.gml", IlivalidatorFileType.TransferFileXtf)]
    public void TransferFileTypeFollowsTheItfExtension(string fileName, IlivalidatorFileType expected)
    {
        var file = new PipelineFile(Path.Combine("TestData", "Ilitools", fileName), fileName);

        Assert.AreEqual(expected, IlivalidatorClient.TransferFileType(file));
    }
}
