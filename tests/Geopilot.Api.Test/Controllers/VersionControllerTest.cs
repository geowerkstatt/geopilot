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
        Assert.StartsWith("3.1", result, StringComparison.Ordinal);
    }
}
