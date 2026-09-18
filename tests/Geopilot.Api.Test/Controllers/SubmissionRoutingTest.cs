using Geopilot.Api.Authorization;

namespace Geopilot.Api.Controllers;

/// <summary>
/// The machine delivery resources must exist only where the installation offers the capability. Asserted
/// through the published API document, because it is generated from the same application model the convention
/// removes the controller from: no entry there means no route either.
/// </summary>
[TestClass]
public class SubmissionRoutingTest
{
    private const string SubmissionPath = "/api/v1/submission";

    [TestMethod]
    public async Task ResourcesArePublishedWhenMachineDeliveryIsEnabled()
    {
        var document = await GetApiDocumentAsync(machineDeliveryEnabled: true);

        Assert.IsTrue(
            document.Contains(SubmissionPath, StringComparison.OrdinalIgnoreCase),
            "The published API must describe the machine delivery resources.");
    }

    [TestMethod]
    public async Task ResourcesAreAbsentWhenMachineDeliveryIsDisabled()
    {
        var document = await GetApiDocumentAsync(machineDeliveryEnabled: false);

        Assert.IsFalse(
            document.Contains(SubmissionPath, StringComparison.OrdinalIgnoreCase),
            "A capability the installation does not offer must be absent from the published API and from routing.");
    }

    private static async Task<string> GetApiDocumentAsync(bool machineDeliveryEnabled)
    {
        using var app = new MachineDeliveryTestApp(machineDeliveryEnabled);
        using var client = app.CreateClient();
        return await client.GetStringAsync("/swagger/all/swagger.json");
    }
}
