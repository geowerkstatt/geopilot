using Geopilot.Api.Controllers;
using Geopilot.Api.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;
using System.Text.Json;

namespace Geopilot.Api;

[TestClass]
public class HealthEndpointsTest
{
    [TestMethod]
    public async Task LiveAnswersHealthyWithoutRunningAnyCheck()
    {
        using var installation = new MachineDeliveryTestApp(backend: UploadBackend.Cloud);
        using var client = installation.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("Healthy", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task ReadyReportsDatabaseAndStorageAsJson()
    {
        using var installation = new MachineDeliveryTestApp(backend: UploadBackend.Cloud);
        using var client = installation.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var checks = await ReadCheckNamesAsync(response);
        CollectionAssert.AreEquivalent(new[] { "Database", "Storage" }, checks);
    }

    [TestMethod]
    public async Task ReadyIncludesTheUploadDirectoryInDirectMode()
    {
        using var installation = new MachineDeliveryTestApp(backend: UploadBackend.Direct);
        using var client = installation.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var checks = await ReadCheckNamesAsync(response);
        CollectionAssert.AreEquivalent(new[] { "Database", "Storage", "UploadDirectory" }, checks);
    }

    [TestMethod]
    public async Task HealthKeepsItsPlainTextAnswer()
    {
        using var installation = new MachineDeliveryTestApp(backend: UploadBackend.Cloud);
        using var client = installation.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("Healthy", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task JsonLeavesOutTheDescriptionOfACheckThatThrew()
    {
        // The framework turns the exception message into the description, and it can carry a host or a path.
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["Database"] = new(HealthStatus.Unhealthy, "Failed to connect to 10.0.0.5:5432", TimeSpan.Zero, new InvalidOperationException("Failed to connect to 10.0.0.5:5432"), null),
                ["Storage"] = new(HealthStatus.Unhealthy, "Missing: Storage:AssetsDirectory", TimeSpan.Zero, null, null),
            },
            TimeSpan.Zero);
        using var responseBody = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Response.Body = responseBody;

        await HealthEndpoints.WriteJsonAsync(context, report);

        var body = System.Text.Encoding.UTF8.GetString(responseBody.ToArray());
        Assert.DoesNotContain("10.0.0.5", body);
        StringAssert.Contains(body, "Missing: Storage:AssetsDirectory");
    }

    private static async Task<List<string>> ReadCheckNamesAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual("Healthy", document.RootElement.GetProperty("status").GetString());
        return document.RootElement.GetProperty("checks").EnumerateObject().Select(check => check.Name).ToList();
    }
}
