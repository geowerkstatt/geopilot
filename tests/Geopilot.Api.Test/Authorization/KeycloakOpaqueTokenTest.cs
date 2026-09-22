using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Runs the Opaque token path against the Keycloak container from docker-compose.yml.
/// </summary>
[TestClass]
public class KeycloakOpaqueTokenTest
{
    private const string RealmUrl = "http://localhost:4011/realms/geopilot";
    private const string TokenUrl = RealmUrl + "/protocol/openid-connect/token";
    private const string PublicClientId = "geopilot-client";
    private const string ApiAudience = "geopilot-api";
    private const string ConfidentialClientId = "geopilot-api";
    private const string ConfidentialClientSecret = "geopilot-api-secret";
    private const string UploaderUsername = "uploader";
    private const string UploaderPassword = "geopilot_password";

    private static readonly TimeSpan KeycloakStartupTimeout = TimeSpan.FromSeconds(120);

    private static KeycloakOpaqueTestApp app = null!;
    private static HttpClient client = null!;
    private static HttpClient keycloak = null!;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        keycloak = new HttpClient();
        await WaitForKeycloakAsync();

        app = new KeycloakOpaqueTestApp();
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
        keycloak?.Dispose();
    }

    [TestMethod]
    public async Task UserSelfWithApiAudienceReturnsUser()
    {
        var token = await GetUploaderTokenAsync("openid geopilot.api");

        using var request = CreateRequest("/api/v1/user/self", token);
        var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual("uploader@geopilot.ch", user.RootElement.GetProperty("email").GetString());
        Assert.AreEqual("Ursula Uploader", user.RootElement.GetProperty("fullName").GetString());
    }

    [TestMethod]
    public async Task UserSelfWithValidTokenForOtherAudienceReturns401()
    {
        var token = await GetUploaderTokenAsync("openid");
        Assert.DoesNotContain(ApiAudience, new JwtSecurityTokenHandler().ReadJwtToken(token).Audiences, "The token must not carry the API audience.");
        using var userInfoRequest = CreateRequest($"{RealmUrl}/protocol/openid-connect/userinfo", token);
        var userInfoResponse = await keycloak.SendAsync(userInfoRequest);
        Assert.AreEqual(HttpStatusCode.OK, userInfoResponse.StatusCode, "Keycloak must accept the token as valid.");

        using var request = CreateRequest("/api/v1/user/self", token);
        var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage CreateRequest(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<string> GetUploaderTokenAsync(string scope)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = PublicClientId,
            ["username"] = UploaderUsername,
            ["password"] = UploaderPassword,
            ["scope"] = scope,
        });

        var response = await keycloak.PostAsync(TokenUrl, content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Token request failed: {body}");

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    private static async Task WaitForKeycloakAsync()
    {
        var deadline = DateTime.UtcNow + KeycloakStartupTimeout;
        while (true)
        {
            try
            {
                var response = await keycloak.GetAsync($"{RealmUrl}/.well-known/openid-configuration");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Keycloak realm at {RealmUrl} was not reachable within {KeycloakStartupTimeout.TotalSeconds} seconds. Start it with docker compose up keycloak.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    private sealed class KeycloakOpaqueTestApp : GeopilotTestApp
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Auth:AccessTokenFormat", "Opaque");
            builder.UseSetting("Auth:IntrospectionUrl", $"{RealmUrl}/protocol/openid-connect/token/introspect");
            builder.UseSetting("Auth:IntrospectionAuthMethod", "ClientSecretBasic");
            builder.UseSetting("Auth:ConfidentialClientId", ConfidentialClientId);
            builder.UseSetting("Auth:ConfidentialClientSecret", ConfidentialClientSecret);
            builder.UseSetting("Auth:Audience", ApiAudience);
            builder.UseSetting("Auth:UserInfoUrl", $"{RealmUrl}/protocol/openid-connect/userinfo");

            base.ConfigureWebHost(builder);
        }
    }
}
