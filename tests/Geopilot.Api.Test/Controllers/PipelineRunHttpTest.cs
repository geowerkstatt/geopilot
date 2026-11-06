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
        using var installation = new JwtTestApp();
        using var client = installation.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestTokenBuilder.CreateValidAdminToken());

        var response = await client.GetAsync($"/api/v1/pipelinerun?state={state}&page=0&pageSize=25");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    [DataRow("person")]
    [DataRow("machineClient")]
    public async Task ListTakesTheKindOfDeclarerTheAdministrationSends(string declarerKind)
    {
        using var installation = new JwtTestApp();
        using var client = installation.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestTokenBuilder.CreateValidAdminToken());

        var response = await client.GetAsync($"/api/v1/pipelinerun?declarerKind={declarerKind}&page=0&pageSize=25");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
