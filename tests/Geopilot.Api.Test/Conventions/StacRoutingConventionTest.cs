namespace Geopilot.Api.Conventions;

[TestClass]
public class StacRoutingConventionTest
{
    [TestMethod]
    [DataRow("GET", "collections/{collectionId}", true)]
    [DataRow("HEAD", "collections", true)]
    [DataRow("POST", "search", true)]
    [DataRow("POST", "api/stac/search/", true)]
    [DataRow("POST", "api/stac/SEARCH", true)]
    [DataRow("POST", "collections/{collectionId}/research", false)]
    [DataRow("POST", "collections/{collectionId}/items", false)]
    [DataRow("POST", null, false)]
    [DataRow("PUT", "collections/{collectionId}", false)]
    [DataRow("DELETE", "collections/{collectionId}/items/{featureId}", false)]
    public void IsRead(string httpMethod, string? template, bool expected)
    {
        Assert.AreEqual(expected, StacRoutingConvention.IsRead(httpMethod, template));
    }
}
