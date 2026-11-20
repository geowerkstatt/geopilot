using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
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
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        client?.Dispose();
        app?.Dispose();
        baseApp?.Dispose();
    }

    [TestMethod]
    public async Task TokenWithRequiredClaimSucceeds()
    {
        var token = JwtTestTokenBuilder.CreateTokenWithClaims(JwtTestTokenBuilder.UserSub, new Claim(ClaimName, ClaimValue));

        var response = await SendAsync("/api/v1/user/self", token);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("tenant-b")]
    [DataRow("TENANT-A")]
    public async Task TokenWithoutRequiredClaimReturns401AndCreatesNoUser(string? value)
    {
        var claims = value is null ? Array.Empty<Claim>() : [new Claim(ClaimName, value)];
        var token = JwtTestTokenBuilder.CreateTokenWithClaims(JwtTestTokenBuilder.UserSub, claims);
        var mockUserInfo = Mock.Get(app.Services.GetRequiredService<IGeopilotUserInfoService>());
        mockUserInfo.Invocations.Clear();

        var response = await SendAsync("/api/v1/user/self", token);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        mockUserInfo.Verify(s => s.GetUserInfoAsync(token, It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task AnonymousEndpointTreatsTokenWithoutRequiredClaimAsNoToken()
    {
        const string adminOnlyCollection = "/api/stac/collections/coll_1";
        var adminWithClaim = JwtTestTokenBuilder.CreateTokenWithClaims(JwtTestTokenBuilder.AdminSub, new Claim(ClaimName, ClaimValue));
        var adminWithoutClaim = JwtTestTokenBuilder.CreateTokenWithClaims(JwtTestTokenBuilder.AdminSub);

        var withClaim = await SendAsync(adminOnlyCollection, adminWithClaim);
        var withoutClaim = await SendAsync(adminOnlyCollection, adminWithoutClaim);

        Assert.AreEqual(HttpStatusCode.OK, withClaim.StatusCode);
        Assert.AreNotEqual(HttpStatusCode.OK, withoutClaim.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(string url, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }
}
