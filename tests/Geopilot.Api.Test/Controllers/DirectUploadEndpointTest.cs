using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Geopilot.Api.Controllers;

[TestClass]
public class DirectUploadEndpointTest
{
    // Just above the application's global 100 MB request body cap, which the upload endpoint is exempt from.
    private const int OverGlobalLimitBytes = 101 * 1024 * 1024;

    private DirectModeTestApp app;
    private HttpClient client;

    [TestInitialize]
    public void Initialize()
    {
        app = new DirectModeTestApp();
        client = app.CreateClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        client.Dispose();
        app.Dispose();
    }

    [TestMethod]
    public async Task UploadsAllRegisteredFilesOfASession()
    {
        var session = await InitiateAsync(("data.xtf", 9), ("beilage.pdf", 4));

        Assert.HasCount(2, session.Files);
        foreach (var (info, content) in session.Files.Zip(new[] { "xtfxtfxtf", "pdf4" }))
        {
            var response = await PutContentAsync(info.UploadUrl, content);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        var storedFiles = Directory.GetFiles(Path.Combine(app.RootDirectory, "uploads", session.UploadId.ToString()));
        Assert.HasCount(2, storedFiles);
        Assert.AreEqual("xtfxtfxtf", File.ReadAllText(storedFiles.Single(f => f.EndsWith("data.xtf", StringComparison.Ordinal))));
    }

    [TestMethod]
    public async Task RejectsContentSmallerThanDeclaredAndStoresNothing()
    {
        var session = await InitiateAsync(("data.xtf", 100));

        var response = await PutContentAsync(session.Files[0].UploadUrl, "way too short");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsFalse(File.Exists(Path.Combine(app.RootDirectory, "uploads", session.UploadId.ToString(), "data.xtf")));
    }

    [TestMethod]
    public async Task RejectsContentLargerThanDeclaredAndStoresNothing()
    {
        var session = await InitiateAsync(("data.xtf", 2));

        var response = await PutContentAsync(session.Files[0].UploadUrl, "definitely more than two bytes");

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.IsFalse(File.Exists(Path.Combine(app.RootDirectory, "uploads", session.UploadId.ToString(), "data.xtf")));
    }

    [TestMethod]
    public async Task AcceptsFileLargerThanTheGlobalBodyLimit()
    {
        var session = await InitiateAsync(("big.xtf", OverGlobalLimitBytes));

        using var content = new ByteArrayContent(new byte[OverGlobalLimitBytes]);
        var response = await client.PutAsync(session.Files[0].UploadUrl, content);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var stored = new FileInfo(Path.Combine(app.RootDirectory, "uploads", session.UploadId.ToString(), "big.xtf"));
        Assert.AreEqual(OverGlobalLimitBytes, stored.Length);
    }

    [TestMethod]
    public async Task KeepsGlobalBodyLimitOnOtherEndpoints()
    {
        using var content = new ByteArrayContent(new byte[OverGlobalLimitBytes]);
        var response = await client.PostAsync("/api/v2/upload", content);

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [TestMethod]
    public async Task ReturnsNotFoundForUnknownUploadId()
    {
        var response = await PutContentAsync($"/api/v2/upload/{Guid.NewGuid()}/data.xtf", "x");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task ReturnsNotFoundForFileNotRegisteredInSession()
    {
        var session = await InitiateAsync(("data.xtf", 1));

        var response = await PutContentAsync($"/api/v2/upload/{session.UploadId}/other.xtf", "x");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task RejectsUploadIntoExpiredSession()
    {
        using var expiringApp = new DirectModeTestApp(urlExpiryMinutes: 0);
        using var expiringClient = expiringApp.CreateClient();

        var initiateResponse = await expiringClient.PostAsJsonAsync("/api/v2/upload", new { files = new[] { new { fileName = "data.xtf", size = 1 } } });
        var session = await initiateResponse.Content.ReadFromJsonAsync<InitiateUploadResponse>();
        var response = await PutContentAsync(expiringClient, session!.Files[0].UploadUrl, "x");

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task RouteDoesNotExistInCloudMode()
    {
        using var cloudApp = new JwtTestApp();
        using var cloudClient = cloudApp.CreateClient();

        var response = await PutContentAsync(cloudClient, $"/api/v2/upload/{Guid.NewGuid()}/data.xtf", "x");

        // In cloud mode no endpoint is mapped on this route, so the request falls through to the
        // authorization fallback policy (Admin), which challenges the anonymous caller. The status
        // documents that no upload handler exists here: nothing accepts or writes the content.
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<InitiateUploadResponse> InitiateAsync(params (string FileName, long Size)[] files)
    {
        var response = await client.PostAsJsonAsync("/api/v2/upload", new
        {
            files = files.Select(f => new { fileName = f.FileName, size = f.Size }).ToArray(),
        });

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<InitiateUploadResponse>();
        Assert.IsNotNull(session);
        return session;
    }

    private Task<HttpResponseMessage> PutContentAsync(string url, string content)
        => PutContentAsync(client, url, content);

    private static async Task<HttpResponseMessage> PutContentAsync(HttpClient httpClient, string url, string content)
    {
        using var body = new StringContent(content, Encoding.UTF8);
        return await httpClient.PutAsync(url, body);
    }

    /// <summary>
    /// Boots the real application with the direct upload backend against a temp directory.
    /// The backend flag and directory are passed via <see cref="IWebHostBuilder.UseSetting"/> because
    /// Program.cs reads them in the builder phase, where configuration added through
    /// ConfigureAppConfiguration is not yet visible.
    /// </summary>
    private sealed class DirectModeTestApp : WebApplicationFactory<Context>
    {
        private readonly int urlExpiryMinutes;

        public DirectModeTestApp(int urlExpiryMinutes = 60)
        {
            this.urlExpiryMinutes = urlExpiryMinutes;
        }

        public string RootDirectory { get; } = Path.Combine(Path.GetTempPath(), $"geopilot-direct-endpoint-test-{Guid.NewGuid():N}");

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(RootDirectory))
                Directory.Delete(RootDirectory, true);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSolutionRelativeContentRoot("src/Geopilot.Api", "*.slnx");
            builder.UseSetting("Upload:Backend", "Direct");
            builder.UseSetting("Upload:Direct:Directory", RootDirectory);
            builder.UseSetting("Upload:RateLimitRequests", "1000");

            builder.ConfigureAppConfiguration((ctx, config) =>
            {
                var pipelineDefinition = Path.Combine(ctx.HostingEnvironment.ContentRootPath, "PipelineDefinitions", "basicPipeline_01.yaml");
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Context"] = TestDatabaseFixture.ConnectionString,
                    ["Pipeline:Definition"] = pipelineDefinition,
                    ["Upload:CleanupIntervalMinutes"] = "1440",
                    ["Upload:UploadUrlExpiryMinutes"] = urlExpiryMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });
            });
        }
    }
}
