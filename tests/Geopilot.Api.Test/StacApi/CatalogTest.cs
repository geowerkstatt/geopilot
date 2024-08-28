using Newtonsoft.Json.Schema;
using Stac;
using Stac.Schemas;

namespace Geopilot.Api.StacApi;

[TestClass]
public class CatalogTest
{
    private GeopilotApiApp app;
    private HttpClient httpClient;

    [TestInitialize]
    public void Initialize()
    {
        app = new GeopilotApiApp();
        httpClient = app.CreateClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        app.Dispose();
        httpClient.Dispose();
    }

    [TestMethod]
    public async Task ValidateCatalogAsync()
    {
        var stacValidator = new StacValidator(new JSchemaUrlResolver());
        const string catalogUri = "/";

        var json = await httpClient.GetStringAsync(catalogUri);

        Assert.IsTrue(stacValidator.ValidateJson(json));

        var catalog = StacConvert.Deserialize<StacCatalog>(json);
        Assert.AreEqual("geopilot", catalog.Id);
        Assert.AreEqual("1.0.0", catalog.StacVersion);
    }
}
