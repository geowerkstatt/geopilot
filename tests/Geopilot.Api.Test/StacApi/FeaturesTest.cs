using Stac.Api.Clients.Features;

namespace Geopilot.Api.StacApi;

[TestClass]
public class FeaturesTest
{
    private GeopilotApiApp app;
    private HttpClient httpClient;
    private FeaturesClient featuresClient;

    [TestInitialize]
    public void Initialize()
    {
        app = new GeopilotApiApp();
        httpClient = app.CreateClient();
        featuresClient = new FeaturesClient(httpClient);
    }

    [TestCleanup]
    public void Cleanup()
    {
        app.Dispose();
        httpClient.Dispose();
    }

    [TestMethod]
    public async Task GetFeaturesAsync()
    {
        var featureCollection = await featuresClient.GetFeaturesAsync("coll_1", null, null, null);

        Assert.AreEqual(2, featureCollection.NumberMatched);
        CollectionAssert.AreEqual(new[] { "item_6", "item_14" }, featureCollection.Features.Select(f => f.Id).ToList());
    }

    [TestMethod]
    public async Task GetFeatureByIdAsync()
    {
        var feature = await featuresClient.GetFeatureAsync("coll_1", "item_14");

        Assert.AreEqual("Datenlieferung_2023-07-04T13:08:47", feature.Title);
        Assert.AreEqual(3, feature.Assets.Count);
    }
}
