using Geopilot.Api.Authorization;
using System.Text.Json;

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

    [TestMethod]
    public async Task MultipartRouteDescribesItsFormInTheApiDocument()
    {
        // The action reads the form from the stream itself, so nothing is bound for Swashbuckle to describe.
        // The operation filter fills the gap; without it a generated client has no way to send this request.
        using var document = JsonDocument.Parse(await GetApiDocumentAsync(machineDeliveryEnabled: true));

        var path = document.RootElement.GetProperty("paths").EnumerateObject()
            .Single(p => string.Equals(p.Name, SubmissionPath + "/multipart", StringComparison.OrdinalIgnoreCase));
        var media = path.Value.GetProperty("post").GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data");
        Assert.AreEqual("#/components/schemas/SubmissionMultipartRequest", media.GetProperty("schema").GetProperty("$ref").GetString());

        var form = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("SubmissionMultipartRequest");
        var properties = form.GetProperty("properties");
        CollectionAssert.AreEquivalent(
            new[] { "mandateKey", "partialDelivery", "precursorDeliveryId", "comment", "file" },
            properties.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.AreEqual("array", properties.GetProperty("file").GetProperty("type").GetString());
        Assert.AreEqual("binary", properties.GetProperty("file").GetProperty("items").GetProperty("format").GetString());
        CollectionAssert.AreEquivalent(
            new[] { "mandateKey", "file" },
            form.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToArray());
    }

    private static async Task<string> GetApiDocumentAsync(bool machineDeliveryEnabled)
    {
        using var app = new MachineDeliveryTestApp(machineDeliveryEnabled);
        using var client = app.CreateClient();
        return await client.GetStringAsync("/swagger/all/swagger.json");
    }
}
