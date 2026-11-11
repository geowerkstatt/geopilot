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

    private static async Task<HttpResponseMessage> GetListAsync(string query)
    {
        using var installation = new JwtTestApp();
        using var client = installation.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestTokenBuilder.CreateValidAdminToken());

        return await client.GetAsync($"/api/v1/pipelinerun?{query}");
    }
}
