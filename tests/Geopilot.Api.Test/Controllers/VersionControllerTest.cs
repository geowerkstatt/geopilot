namespace Geopilot.Api.Controllers;

[TestClass]
public class VersionControllerTest
{
    private const string AppVersion = "3.1";

    [TestMethod]
    public void GetVersion()
    {
        var result = new VersionController().Get();
        Assert.IsNotNull(result);
        Assert.IsGreaterThan(0, result.Length);
        Assert.StartsWith(AppVersion, result, StringComparison.Ordinal);
    }

    [TestMethod]
    public void GetShortVersion()
    {
        var result = VersionController.GetShortVersion();
        Assert.AreEqual(AppVersion, result);
    }
}
