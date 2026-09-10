using Geopilot.Api.Processing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;

namespace Geopilot.Api.Authorization;

[TestClass]
public class OpaqueTokenSecurityTest
{
    private static OpaqueTestApp app = null!;
    private static HttpClient client = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        app = new OpaqueTestApp();
        client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        client?.Dispose();
        app?.Dispose();
    }

    [TestMethod]
    public async Task ProtectedEndpointWithoutTokenReturns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/user");
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task InactiveTokenOnAdminEndpointReturns401()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/user", OpaqueTestApp.OpaqueInactiveToken);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task ActiveUserTokenOnAdminEndpointReturns403()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/user", OpaqueTestApp.OpaqueUserToken);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task ActiveAdminTokenOnUserEndpointReturns200()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/user", OpaqueTestApp.OpaqueAdminToken);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task ActiveAdminTokenOnUserSelfEndpointReturns200()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/user/self", OpaqueTestApp.OpaqueAdminToken);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task ActiveAdminTokenOnMandateSummaryEndpointReturnsNon500()
    {
        var uploadStore = app.Services.GetRequiredService<IUploadStore>();
        var uploadId = Guid.NewGuid();
        uploadStore.CreateUpload(uploadId, ImmutableList.Create(new UploadedFileInfo("test.xtf", "key", 10)));

        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"/api/v1/mandate/summary?uploadId={uploadId}", OpaqueTestApp.OpaqueAdminToken);
            var response = await client.SendAsync(request);
            Assert.AreNotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        finally
        {
            uploadStore.RemoveUpload(uploadId);
        }
    }

    [TestMethod]
    public async Task RequestToUserSelfProducesExactlyOneUserInfoHttpCall()
    {
        using var testApp = new OpaqueTestApp();
        using var testClient = testApp.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var request = CreateRequest(HttpMethod.Get, "/api/v1/user/self", OpaqueTestApp.OpaqueAdminToken);
        var response = await testClient.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(1, testApp.UserInfoHttpCallCount);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? token = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }
}
