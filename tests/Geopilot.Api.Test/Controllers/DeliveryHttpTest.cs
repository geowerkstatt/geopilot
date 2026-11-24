using Geopilot.Api.Authorization;
using System.Net;
using System.Net.Http.Headers;

namespace Geopilot.Api.Controllers;

/// <summary>
/// The list of deliveries as the administration calls it, through routing and model binding. The state values are
/// the camelCase names the API document gives the enum, which is what the generated frontend types send.
/// </summary>
[TestClass]
public class DeliveryHttpTest
{
    [TestMethod]
    [DataRow("failedReactionKinds=transfer")]
    [DataRow("failedReactionKinds=transfer&failedReactionKinds=notification")]
    [DataRow("idPrefix=123456789")]
    [DataRow("from=2026-10-28T00:00:00Z&to=2026-10-28T00:00:00Z")]
    public async Task ListTakesWhatTheAdministrationSends(string query)
    {
        var response = await GetListAsync($"{query}&page=0&pageSize=25");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task ListRejectsAPeriodThatEndsBeforeItStarts()
    {
        var response = await GetListAsync("from=2026-10-28T00:00:00Z&to=2026-10-27T23:59:59Z&page=0&pageSize=25");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    [DataRow("page=-1&pageSize=25")]
    [DataRow("page=1000001&pageSize=25")]
    [DataRow("page=0&pageSize=0")]
    [DataRow("page=0&pageSize=101")]
    public async Task ListRejectsAPageOutOfBounds(string query)
    {
        var response = await GetListAsync(query);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    [DataRow("1a")]
    [DataRow("1234567890")]
    [DataRow("-12")]
    [DataRow("1%25")]
    public async Task ListRejectsAnIdPrefixThatIsNotOneToNineDigits(string idPrefix)
    {
        var response = await GetListAsync($"idPrefix={idPrefix}&page=0&pageSize=25");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task ListRejectsAnUnknownReactionKind()
    {
        var response = await GetListAsync("failedReactionKinds=notAKind&page=0&pageSize=25");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, "A kind the binder does not know must not be dropped silently.");
    }

    [TestMethod]
    [DataRow("mandateIds")]
    [DataRow("userIds")]
    [DataRow("machineClientIds")]
    [DataRow("failedReactionKinds")]
    public async Task ListTakesAHundredValuesOfAFilterAndNoMore(string filter)
    {
        var value = filter == "failedReactionKinds" ? "transfer" : "1";
        string Values(int count) => string.Join("&", Enumerable.Repeat($"{filter}={value}", count));

        var hundred = await GetListAsync($"{Values(100)}&page=0&pageSize=25");
        var hundredAndOne = await GetListAsync($"{Values(101)}&page=0&pageSize=25");

        Assert.AreEqual(HttpStatusCode.OK, hundred.StatusCode, await hundred.Content.ReadAsStringAsync());
        Assert.AreEqual(HttpStatusCode.BadRequest, hundredAndOne.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetListAsync(string query)
    {
        using var installation = new JwtTestApp();
        using var client = installation.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestTokenBuilder.CreateValidAdminToken());

        return await client.GetAsync($"/api/v1/delivery?{query}");
    }
}
