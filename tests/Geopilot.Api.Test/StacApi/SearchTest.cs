using Stac.Api.Clients.ItemSearch;

namespace Geopilot.Api.StacApi;

[TestClass]
public class SearchTest
{
    private GeopilotApiApp app;
    private HttpClient httpClient;
    private ItemSearchClient searchClient;

    [TestInitialize]
    public void Initialize()
    {
        app = new GeopilotApiApp();
        httpClient = app.CreateClient();
        searchClient = new ItemSearchClient(httpClient);
    }

    [TestCleanup]
    public void Cleanup()
    {
        app.Dispose();
        httpClient.Dispose();
    }

    [TestMethod]
    public async Task SearchCollection1()
    {
        var result = await searchClient.PostItemSearchAsync(new SearchBody
        {
            Collections = new[] { "coll_1" },
            Limit = 12,
        });

        Assert.AreEqual(2, result.Items.Count());
        Assert.AreEqual(2, result.NumberMatched);
        Assert.AreEqual(2, result.NumberReturned);

        CollectionAssert.AreEqual(new[] { "item_6", "item_14" }, result.Items.Select(i => i.Id).ToList());
    }

    [TestMethod]
    public async Task SearchLimit()
    {
        const int limit = 8;
        var result = await searchClient.PostItemSearchAsync(new SearchBody
        {
            Collections = Array.Empty<string>(),
            Limit = limit,
        });

        Assert.AreEqual(limit, result.Items.Count());
        Assert.AreEqual(20, result.NumberMatched);
        Assert.AreEqual(limit, result.NumberReturned);
    }
}
