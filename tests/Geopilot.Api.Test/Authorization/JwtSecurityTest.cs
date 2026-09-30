using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Headers;

namespace Geopilot.Api.Authorization;

[TestClass]
public class JwtSecurityTest
{
    private const string RepresentativeAdminEndpoint = "/api/v1/user";
    private const string RepresentativeAdminMethod = "GET";

    private static JwtTestApp app;
    private static HttpClient client;

    public static IEnumerable<object[]> ProtectedEndpoints => EndpointDiscovery.GetProtectedEndpoints();

    public static IEnumerable<object[]> AdminEndpoints => EndpointDiscovery.GetAdminEndpoints();

    public static IEnumerable<object[]> UserEndpoints => EndpointDiscovery.GetUserEndpoints();

    public static IEnumerable<object[]> DeclarerEndpoints => EndpointDiscovery.GetDeclarerEndpoints();

    public static IEnumerable<object[]> AnonymousEndpoints => EndpointDiscovery.GetAnonymousEndpoints();

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        app = new JwtTestApp();
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
    [DynamicData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpointNoTokenReturns401(string method, string url, string policy, string description)
    {
        using var request = CreateRequest(method, url);
        var response = await client.SendAsync(request);
        Assert.AreEqual(
            HttpStatusCode.Unauthorized,
            response.StatusCode,
            $"{description}: Expected 401 without token but got {(int)response.StatusCode}");
    }

    [TestMethod]
    [DynamicData(nameof(AdminEndpoints))]
    public async Task AdminEndpointValidAdminTokenReturnsNon401(string method, string url, string policy, string description)
    {
        var token = JwtTestTokenBuilder.CreateValidAdminToken();
        using var request = CreateRequest(method, url, token);
        var response = await client.SendAsync(request);
        Assert.AreNotEqual(
            HttpStatusCode.Unauthorized,
            response.StatusCode,
            $"{description}: Valid admin token should not return 401");
        Assert.AreNotEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode,
            $"{description}: Valid admin token should not return 403");
    }

    [TestMethod]
    [DynamicData(nameof(AdminEndpoints))]
    public async Task AdminEndpointValidUserTokenReturns403(string method, string url, string policy, string description)
    {
        var token = JwtTestTokenBuilder.CreateValidUserToken();
        using var request = CreateRequest(method, url, token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode,
            $"{description}: Non-admin should get 403 on admin endpoint but got {(int)response.StatusCode}");
    }

    [TestMethod]
    [DynamicData(nameof(UserEndpoints))]
    public async Task UserEndpointValidUserTokenReturnsNon401(string method, string url, string policy, string description)
    {
        var token = JwtTestTokenBuilder.CreateValidUserToken();
        using var request = CreateRequest(method, url, token);
        var response = await client.SendAsync(request);
        Assert.AreNotEqual(
            HttpStatusCode.Unauthorized,
            response.StatusCode,
            $"{description}: Valid user token should not return 401");
        Assert.AreNotEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode,
            $"{description}: Valid user token should not return 403");
    }

    [TestMethod]
    [DynamicData(nameof(UserEndpoints))]
    public async Task UserEndpointValidClientTokenReturns403(string method, string url, string policy, string description)
    {
        var token = JwtTestTokenBuilder.CreateValidClientToken();
        using var request = CreateRequest(method, url, token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode,
            $"{description}: A machine client is not a user, so its credentials must not reach anything a person uses, but got {(int)response.StatusCode}");
    }

    [TestMethod]
    [DynamicData(nameof(AdminEndpoints))]
    public async Task AdminEndpointValidClientTokenReturns403(string method, string url, string policy, string description)
    {
        var token = JwtTestTokenBuilder.CreateValidClientToken();
        using var request = CreateRequest(method, url, token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode,
            $"{description}: A machine client must not reach an admin endpoint but got {(int)response.StatusCode}");
    }

    [TestMethod]
    [DynamicData(nameof(DeclarerEndpoints))]
    public async Task DeclarerEndpointValidUserTokenReturnsNon401(string method, string url, string policy, string description)
    {
        var token = JwtTestTokenBuilder.CreateValidUserToken();
        using var request = CreateRequest(method, url, token);
        var response = await client.SendAsync(request);
        Assert.AreNotEqual(
            HttpStatusCode.Unauthorized,
            response.StatusCode,
            $"{description}: Valid user token should not return 401");
        Assert.AreNotEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode,
            $"{description}: Valid user token should not return 403");
    }

    [TestMethod]
    [DynamicData(nameof(DeclarerEndpoints))]
    public async Task DeclarerEndpointValidClientTokenReturnsNon401(string method, string url, string policy, string description)
    {
        var token = JwtTestTokenBuilder.CreateValidClientToken();
        using var request = CreateRequest(method, url, token);
        var response = await client.SendAsync(request);
        Assert.AreNotEqual(
            HttpStatusCode.Unauthorized,
            response.StatusCode,
            $"{description}: Valid client token should not return 401");
        Assert.AreNotEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode,
            $"{description}: A registered machine client should not return 403 on the machine delivery");
    }

    [TestMethod]
    [DynamicData(nameof(AnonymousEndpoints))]
    public async Task AnonymousEndpointNoTokenReturnsNon401(string method, string url, string description)
    {
        using var request = CreateRequest(method, url);
        var response = await client.SendAsync(request);
        Assert.AreNotEqual(
            HttpStatusCode.Unauthorized,
            response.StatusCode,
            $"{description}: Anonymous endpoint should not return 401 without token");
    }

    [TestMethod]
    [DynamicData(nameof(AnonymousEndpoints))]
    public async Task AnonymousEndpointValidTokenReturnsNon401(string method, string url, string description)
    {
        var token = JwtTestTokenBuilder.CreateValidAdminToken();
        using var request = CreateRequest(method, url, token);
        var response = await client.SendAsync(request);
        Assert.AreNotEqual(
            HttpStatusCode.Unauthorized,
            response.StatusCode,
            $"{description}: Anonymous endpoint should accept valid tokens too");
    }

    [TestMethod]
    [DynamicData(nameof(AnonymousEndpoints))]
    public async Task AnonymousEndpointUnavailableIdpReturnsNon503(string method, string url, string description)
    {
        var token = JwtTestTokenBuilder.CreateIdpUnavailableToken();
        using var request = CreateRequest(method, url, token);
        var response = await client.SendAsync(request);
        Assert.AreNotEqual(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode,
            $"{description}: Anonymous endpoint should not return 503 when IdP is unavailable");
        Assert.AreNotEqual(
            HttpStatusCode.Unauthorized,
            response.StatusCode,
            $"{description}: Anonymous endpoint should accept valid tokens too");
    }

    [TestMethod]
    public async Task AnonymousEndpointWithTokenDoesNotCallUserInfo()
    {
        var mockUserInfo = Mock.Get(app.Services.GetRequiredService<IGeopilotUserInfoService>());
        mockUserInfo.Invocations.Clear();

        var token = JwtTestTokenBuilder.CreateValidAdminToken();
        using var request = CreateRequest("GET", "/api/v1/version", token);
        var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        mockUserInfo.Verify(s => s.GetUserInfoAsync(token, It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task UnavailableIdpReturns503()
    {
        var token = JwtTestTokenBuilder.CreateIdpUnavailableToken();
        using var request = CreateRequest(RepresentativeAdminMethod, RepresentativeAdminEndpoint, token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("WWW-Authenticate"));
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "Authentication currently not possible.");
    }

    [TestMethod]
    public async Task ExpiredTokenReturns401()
    {
        var token = JwtTestTokenBuilder.CreateExpiredToken();
        using var request = CreateRequest(RepresentativeAdminMethod, RepresentativeAdminEndpoint, token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task FutureNbfTokenReturns401()
    {
        var token = JwtTestTokenBuilder.CreateFutureNbfToken();
        using var request = CreateRequest(RepresentativeAdminMethod, RepresentativeAdminEndpoint, token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task WrongAudienceReturns401()
    {
        var token = JwtTestTokenBuilder.CreateWrongAudienceToken();
        using var request = CreateRequest(RepresentativeAdminMethod, RepresentativeAdminEndpoint, token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task WrongIssuerReturns401()
    {
        var token = JwtTestTokenBuilder.CreateWrongIssuerToken();
        using var request = CreateRequest(RepresentativeAdminMethod, RepresentativeAdminEndpoint, token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task WrongSigningKeyReturns401()
    {
        var token = JwtTestTokenBuilder.CreateWrongKeyToken();
        using var request = CreateRequest(RepresentativeAdminMethod, RepresentativeAdminEndpoint, token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task TamperedSignatureReturns401()
    {
        var token = JwtTestTokenBuilder.CreateTamperedToken();
        using var request = CreateRequest(RepresentativeAdminMethod, RepresentativeAdminEndpoint, token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    [DataRow("none")]
    [DataRow("None")]
    [DataRow("NONE")]
    [DataRow("nOnE")]
    public async Task AlgNoneReturns401(string alg)
    {
        var token = JwtTestTokenBuilder.CreateAlgNoneToken(alg);
        using var request = CreateRequest(RepresentativeAdminMethod, RepresentativeAdminEndpoint, token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task AlgHS256KeyConfusionReturns401()
    {
        var token = JwtTestTokenBuilder.CreateHS256KeyConfusionToken();
        using var request = CreateRequest(RepresentativeAdminMethod, RepresentativeAdminEndpoint, token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task ValidAdminTokenUserEndpointSucceeds()
    {
        var token = JwtTestTokenBuilder.CreateValidAdminToken();
        using var request = CreateRequest("GET", "/api/v1/user/self", token);
        var response = await client.SendAsync(request);
        Assert.AreNotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreNotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task ValidAdminTokenAdminEndpointReturnsSuccessStatus()
    {
        var token = JwtTestTokenBuilder.CreateValidAdminToken();
        using var request = CreateRequest("GET", "/api/v1/user", token);
        var response = await client.SendAsync(request);
        Assert.AreEqual(
            HttpStatusCode.OK,
            response.StatusCode,
            "Expected 200 for valid admin token on GET /api/v1/user");
    }

    [TestMethod]
    public void EndpointDiscoveryFindsEndpoints()
    {
        var allEndpoints = EndpointDiscovery.GetProtectedEndpoints()
            .Concat(EndpointDiscovery.GetAnonymousEndpoints())
            .ToList();

        Assert.IsNotEmpty(allEndpoints);
    }

    private static HttpRequestMessage CreateRequest(string method, string url, string? token = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }
}
