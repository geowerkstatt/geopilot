namespace Geopilot.Api.Controllers;

[TestClass]
public class VersionControllerTest
{
    [TestMethod]
    public void GetVersion()
    {
        var result = new VersionController().Get();
        Assert.IsNotNull(result);
        Assert.IsGreaterThan(0, result.Length);
        StringAssert.StartsWith(result, "3.0", StringComparison.Ordinal);
    }
}
