using Stac.Api;
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
        httpClient = app.CreateAdminClient();
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

    [TestMethod]
    public async Task AnonymousSeesOnlyPublicCollections()
    {
        using var anonymousClient = app.CreateClient();
        var collections = await new CollectionsClient(anonymousClient).GetCollectionsAsync();

        CollectionAssert.AreEqual(new[] { "coll_10" }, collections.Collections.Select(c => c.Id).ToList());
    }

    [TestMethod]
    public async Task AnonymousCannotDescribeAnAdminOnlyCollection()
    {
        using var anonymousClient = app.CreateClient();
        var anonymousCollectionsClient = new CollectionsClient(anonymousClient);

        var adminOnly = await Assert.ThrowsExactlyAsync<StacApiException>(() => anonymousCollectionsClient.DescribeCollectionAsync("coll_1"));
        var nonExistent = await Assert.ThrowsExactlyAsync<StacApiException>(() => anonymousCollectionsClient.DescribeCollectionAsync("coll_999999"));
        Assert.AreEqual(nonExistent.StatusCode, adminOnly.StatusCode);
    }
}
