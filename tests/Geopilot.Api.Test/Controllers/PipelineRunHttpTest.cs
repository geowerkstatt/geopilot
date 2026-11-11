using Geopilot.Api.Authorization;
using System.Net;
using System.Net.Http.Headers;

namespace Geopilot.Api.Controllers;

/// <summary>
/// The list of pipeline runs as the administration calls it, through routing and model binding. The filter values are
/// the camelCase names the API document gives the enums, which is what the generated frontend types send.
/// </summary>
[TestClass]
public class PipelineRunHttpTest
{
    [TestMethod]
    [DataRow("success")]
    [DataRow("failed")]
    [DataRow("deliveryRestriction")]
    [DataRow("open")]
    public async Task ListTakesTheStateTheAdministrationSends(string state)
    {
        var response = await GetListAsync($"states={state}&page=0&pageSize=25");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task ListRejectsAnUnknownState()
    {
        var response = await GetListAsync("states=notAState&page=0&pageSize=25");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, "A state the binder does not know must not be dropped silently.");
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
    public async Task ListAnswersTheLastPageItTakes()
    {
        var response = await GetListAsync("page=1000000&pageSize=100");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "The offset of the last page must still fit a whole number.");
    }

    [TestMethod]
    [DataRow("mandateIds")]
    [DataRow("userIds")]
    [DataRow("machineClientIds")]
    [DataRow("states")]
    public async Task ListTakesAHundredValuesOfAFilterAndNoMore(string filter)
    {
        var value = filter == "states" ? "open" : "1";
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

        return await client.GetAsync($"/api/v1/pipelinerun?{query}");
    }
}
