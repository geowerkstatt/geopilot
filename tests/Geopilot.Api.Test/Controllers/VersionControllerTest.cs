namespace Geopilot.Api.Controllers;

[TestClass]
public class VersionControllerTest
{
    [TestMethod]
    public void GetVersion()
    {
        var result = new VersionController().Get();
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Length > 0);
        StringAssert.StartsWith(result, "3.0", StringComparison.Ordinal);
    }
}
