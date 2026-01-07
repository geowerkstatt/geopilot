using Stac.Api.Clients.Collections;

namespace Geopilot.Api.StacApi;

[TestClass]
public class CollectionsTest
{
    private GeopilotApiApp app;
    private HttpClient httpClient;
    private CollectionsClient collectionsClient;

    [TestInitialize]
    public void Initialize()
    {
        app = new GeopilotApiApp();
        httpClient = app.CreateClient();
        collectionsClient = new CollectionsClient(httpClient);
    }

    [TestCleanup]
    public void Cleanup()
    {
        app.Dispose();
        httpClient.Dispose();
    }

    [TestMethod]
    public async Task GetCollectionsAsync()
    {
        var collections = await collectionsClient.GetCollectionsAsync();

        Assert.HasCount(10, collections.Collections);
    }

    [TestMethod]
    public async Task DescribeCollectionAsync()
    {
        var collection = await collectionsClient.DescribeCollectionAsync("coll_1");

        Assert.AreEqual("Handmade Soft Cheese", collection.Title);
    }
}
