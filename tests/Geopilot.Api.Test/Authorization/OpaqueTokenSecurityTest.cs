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

    public static IEnumerable<object[]> DeclarerEndpoints => EndpointDiscovery.GetDeclarerEndpoints();

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        app = new OpaqueTestApp();
        client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var scope = app.Services.CreateScope();
        TestMachineClients.EnsureRegistered(scope.ServiceProvider.GetRequiredService<Context>());
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
    public async Task UnavailableIdpOnAdminEndpointReturns503()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/user", OpaqueTestApp.OpaqueIdpUnavailableToken);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("WWW-Authenticate"));
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "Authentication currently not possible.");
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
    public async Task ActiveClientTokenOnUserSelfEndpointReturns403()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/user/self", OpaqueTestApp.OpaqueClientToken);
        var response = await client.SendAsync(request);
        Assert.AreEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode,
            "A registered machine client authenticates on its introspected subject alone, and is then kept out of everything meant for a person. A 401 here would mean the missing user info stopped it from authenticating at all.");
    }

    [TestMethod]
    [DynamicData(nameof(DeclarerEndpoints))]
    public async Task DeclarerEndpointActiveClientTokenReturnsNon401(string method, string url, string policy, string description)
    {
        // The counterpart of the sweep in JwtSecurityTest: a client credentials token has to reach the machine
        // delivery in this format too, where the subject comes from the introspection answer instead of a claim.
        using var request = CreateRequest(new HttpMethod(method), url, OpaqueTestApp.OpaqueClientToken);
        var response = await client.SendAsync(request);

        Assert.AreNotEqual(HttpStatusCode.Unauthorized, response.StatusCode, $"{description}: Valid client token should not return 401");
        Assert.AreNotEqual(HttpStatusCode.Forbidden, response.StatusCode, $"{description}: A registered machine client should not return 403 on the machine delivery");
    }

    [TestMethod]
    public async Task ActiveClientTokenOnAdminEndpointReturns403()
    {
        using var request = CreateRequest(HttpMethod.Get, "/api/v1/user", OpaqueTestApp.OpaqueClientToken);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
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
    public async Task RequestWithClientTokenProducesNoUserInfoHttpCall()
    {
        using var testApp = new OpaqueTestApp();
        using var testClient = testApp.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var request = CreateRequest(HttpMethod.Get, "/api/v1/user/self", OpaqueTestApp.OpaqueClientToken);
        var response = await testClient.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.AreEqual(
            0,
            testApp.UserInfoHttpCallCount,
            "A registered machine client names no person. Asking the identity provider anyway logs a failed user info request for every call of the machine, and an operator reading that looks for a problem that is not there.");
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
