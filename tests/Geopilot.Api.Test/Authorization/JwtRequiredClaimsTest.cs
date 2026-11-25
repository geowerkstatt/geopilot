using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace Geopilot.Api.Authorization;

[TestClass]
public class JwtRequiredClaimsTest
{
    private const string ClaimName = "urn:zitadel:iam:user:resourceowner:id";
    private const string ClaimValue = "tenant-a";

    private static JwtTestApp baseApp;
    private static WebApplicationFactory<Context> app;
    private static HttpClient client;

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        baseApp = new JwtTestApp();
        app = baseApp.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:RequiredClaims:0:Name", ClaimName);
            builder.UseSetting("Auth:RequiredClaims:0:Value", ClaimValue);
        });
        client = app.CreateClient();

        using var scope = app.Services.CreateScope();
        TestMachineClients.EnsureRegistered(scope.ServiceProvider.GetRequiredService<Context>());
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        client?.Dispose();
        app?.Dispose();
        baseApp?.Dispose();
    }

    [TestMethod]
    [DataRow(ClaimValue, ClaimValueTypes.String)]
    [DataRow("[\"tenant-a\"]", JsonClaimValueTypes.JsonArray)]
    public async Task TokenWithRequiredClaimSucceeds(string value, string valueType)
    {
        var token = JwtTestTokenBuilder.CreateTokenWithClaims(JwtTestTokenBuilder.UserSub, new Claim(ClaimName, value, valueType));

        var response = await SendAsync("/api/v1/user/self", token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("tenant-b", ClaimValueTypes.String)]
    [DataRow("TENANT-A", ClaimValueTypes.String)]
    [DataRow("[\"tenant-b\",\"tenant-a\"]", JsonClaimValueTypes.JsonArray)]
    [DataRow("[\"tenant-a\",\"tenant-a\"]", JsonClaimValueTypes.JsonArray)]
    [DataRow("[]", JsonClaimValueTypes.JsonArray)]
    public async Task TokenWithoutRequiredClaimReturns401AndCreatesNoUser(string? value, string? valueType)
    {
        var claims = value is null ? Array.Empty<Claim>() : [new Claim(ClaimName, value, valueType)];
        var token = JwtTestTokenBuilder.CreateTokenWithClaims(JwtTestTokenBuilder.UserSub, claims);
        var mockUserInfo = Mock.Get(app.Services.GetRequiredService<IGeopilotUserInfoService>());

        var response = await SendAsync("/api/v1/user/self", token);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        mockUserInfo.Verify(s => s.GetUserInfoAsync(token, It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [DataRow(ClaimValueTypes.String, true, HttpStatusCode.OK)]
    [DataRow(ClaimValueTypes.Integer64, true, HttpStatusCode.Unauthorized)]
    [DataRow(null, true, HttpStatusCode.Unauthorized)]
    [DataRow(ClaimValueTypes.String, false, HttpStatusCode.Unauthorized)]
    public async Task EveryRequiredClaimMustMatchWithOneTextValue(string? numberValueType, bool withTenant, HttpStatusCode expected)
    {
        using var appWithTwoClaims = baseApp.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Auth:RequiredClaims:0:Name", "number");
            builder.UseSetting("Auth:RequiredClaims:0:Value", "123");
            builder.UseSetting("Auth:RequiredClaims:1:Name", "tid");
            builder.UseSetting("Auth:RequiredClaims:1:Value", "tenant-a");
        });
        using var clientWithTwoClaims = appWithTwoClaims.CreateClient();
        var claims = new List<Claim>();
        if (numberValueType is not null)
        {
            claims.Add(new Claim("number", "123", numberValueType));
        }

        if (withTenant)
        {
            claims.Add(new Claim("tid", "tenant-a"));
        }

        var token = JwtTestTokenBuilder.CreateTokenWithClaims(JwtTestTokenBuilder.UserSub, [.. claims]);

        var response = await SendAsync(clientWithTwoClaims, "/api/v1/user/self", token);

        Assert.AreEqual(expected, response.StatusCode);
    }

    [TestMethod]
    public async Task MachineClientTokenWithRequiredClaimIsAuthenticated()
    {
        var token = JwtTestTokenBuilder.CreateClientToken(JwtTestTokenBuilder.ClientSub, new Claim(ClaimName, ClaimValue));

        var response = await SendAsync($"/api/v1/submission/{Guid.NewGuid()}", token);

        Assert.AreNotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreNotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("tenant-b")]
    public async Task MachineClientTokenWithoutRequiredClaimReturns401(string? value)
    {
        var claims = value is null ? Array.Empty<Claim>() : [new Claim(ClaimName, value)];
        var token = JwtTestTokenBuilder.CreateClientToken(JwtTestTokenBuilder.ClientSub, claims);

        var response = await SendAsync($"/api/v1/submission/{Guid.NewGuid()}", token);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task AnonymousEndpointTreatsTokenWithoutRequiredClaimAsNoToken()
    {
        const string adminOnlyCollection = "/api/stac/collections/coll_1";
        var adminWithClaim = JwtTestTokenBuilder.CreateTokenWithClaims(JwtTestTokenBuilder.AdminSub, new Claim(ClaimName, ClaimValue));
        var adminWithoutClaim = JwtTestTokenBuilder.CreateTokenWithClaims(JwtTestTokenBuilder.AdminSub);

        var withClaim = await SendAsync(adminOnlyCollection, adminWithClaim);
        var withoutClaim = await SendAsync(adminOnlyCollection, adminWithoutClaim);
        var withoutToken = await SendAsync(adminOnlyCollection);

        Assert.AreEqual(HttpStatusCode.OK, withClaim.StatusCode);
        Assert.AreNotEqual(HttpStatusCode.OK, withoutToken.StatusCode);
        Assert.AreEqual(withoutToken.StatusCode, withoutClaim.StatusCode);
    }

    private static Task<HttpResponseMessage> SendAsync(string url, string? token = null) => SendAsync(client, url, token);

    private static async Task<HttpResponseMessage> SendAsync(HttpClient httpClient, string url, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await httpClient.SendAsync(request);
    }
}
