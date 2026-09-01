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

    [TestMethod]
    public void GetShortVersion()
    {
        var result = VersionController.GetShortVersion();
        Assert.AreEqual("3.0", result);
    }
}
