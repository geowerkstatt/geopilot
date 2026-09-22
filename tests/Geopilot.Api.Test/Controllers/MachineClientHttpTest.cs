using Geopilot.Api.Authorization;
using System.Net.Http.Headers;

namespace Geopilot.Api.Controllers;

[TestClass]
public class MachineClientHttpTest
{
    [TestMethod]
    public async Task DoesNotRouteTheAdministrationWhenMachineDeliveryIsDisabled()
    {
        using var installation = new MachineDeliveryTestApp(machineDeliveryEnabled: false);
        using var client = installation.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestTokenBuilder.CreateValidAdminToken());

        var response = await client.GetAsync("/api/v1/machineclient");
        var neverExisted = await client.GetAsync("/api/v1/aroutethatneverexisted");

        Assert.AreEqual(
            neverExisted.StatusCode,
            response.StatusCode,
            "An installation without machine delivery has no clients to administer, so the route has to be as absent as one that was never defined.");
    }
}
