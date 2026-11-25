using Geopilot.Api.Authorization;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http.Headers;

namespace Geopilot.Api.Controllers;

[TestClass]
public class RefMappingHttpTest
{
    [TestMethod]
    public async Task DoesNotRouteTheAdministrationWhenRefMappingIsDisabled()
    {
        using var app = new JwtTestApp();
        using var installation = app.WithWebHostBuilder(builder => builder.UseSetting("RefMapping:Enabled", "false"));
        using var client = installation.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestTokenBuilder.CreateValidAdminToken());

        var response = await client.GetAsync("/api/v1/refmapping");
        var neverExisted = await client.GetAsync("/api/v1/aroutethatneverexisted");

        Assert.AreEqual(
            neverExisted.StatusCode,
            response.StatusCode,
            "An installation that does not maintain the mapping in the portal must not expose its administration.");
    }
}
