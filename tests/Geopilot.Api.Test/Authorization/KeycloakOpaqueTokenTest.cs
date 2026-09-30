using Geopilot.Api.Enums;
using Geopilot.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Runs the Opaque token path against the Keycloak container from docker-compose.yml, with the token of a person
/// and with the token the client <c>geopilot-api</c> obtains for itself with client credentials.
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

    /// <summary>
    /// The id the realm file pins the service account of <c>geopilot-api</c> to, and the subject its token carries.
    /// </summary>
    private const string ServiceAccountSub = "2d4f8c6e-1a3b-4d5e-9f7a-8b6c5d4e3f2a";
    private const string MandateKey = "KEYCLOAKFALCON";

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

        using var scope = app.Services.CreateScope();
        RegisterServiceAccount(scope.ServiceProvider.GetRequiredService<Context>());
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

    [TestMethod]
    public async Task SubmissionWithClientCredentialsTokenReturns202()
    {
        var token = await GetClientCredentialsTokenAsync();
        Assert.AreEqual(
            ServiceAccountSub,
            new JwtSecurityTokenHandler().ReadJwtToken(token).Subject,
            "The realm file pins the service account of geopilot-api to this id. A keycloak container created before that change still runs the old realm; recreate it so it imports the current file.");

        // The fields come before the file, as the resource requires. The container disposes its parts as well;
        // the analyzer only sees the ones declared here.
        using var mandateKeyPart = new StringContent(MandateKey);
        using var filePart = new ByteArrayContent(Encoding.UTF8.GetBytes(new string('x', 16)));
        filePart.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var content = new MultipartFormDataContent();
        content.Add(mandateKeyPart, "mandateKey");
        content.Add(filePart, "file", "data.xtf");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/submission/multipart") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);

        Assert.AreEqual(
            HttpStatusCode.Accepted,
            response.StatusCode,
            $"The identity provider describes no person for this token (its user info endpoint answers 403), so the introspected subject has to be all the delivery needs. Response: {await response.Content.ReadAsStringAsync()}");
    }

    [TestMethod]
    public async Task UserSelfWithClientCredentialsTokenReturns403()
    {
        var token = await GetClientCredentialsTokenAsync();

        using var request = CreateRequest("/api/v1/user/self", token);
        var response = await client.SendAsync(request);

        Assert.AreEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode,
            "A machine client authenticates and is then no person. A 401 would mean its token was not accepted at all.");
    }

    private static HttpRequestMessage CreateRequest(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static Task<string> GetUploaderTokenAsync(string scope) =>
        RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = PublicClientId,
            ["username"] = UploaderUsername,
            ["password"] = UploaderPassword,
            ["scope"] = scope,
        });

    private static Task<string> GetClientCredentialsTokenAsync() =>
        RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = ConfidentialClientId,
            ["client_secret"] = ConfidentialClientSecret,
        });

    private static async Task<string> RequestTokenAsync(Dictionary<string, string> form)
    {
        using var content = new FormUrlEncodedContent(form);

        var response = await keycloak.PostAsync(TokenUrl, content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Token request failed: {body}");

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    /// <summary>
    /// Registers the service account as the machine client its token is admitted for, and a public mandate it
    /// delivers to. Committed rather than rolled back, because the host reads the database through its own
    /// connection, and guarded so a second run finds both instead of clashing on the unique indexes.
    /// </summary>
    private static void RegisterServiceAccount(Context context)
    {
        if (!context.MachineClients.Any(c => c.AuthIdentifier == ServiceAccountSub))
        {
            context.MachineClients.Add(new MachineClient
            {
                AuthIdentifier = ServiceAccountSub,
                Name = "Service account geopilot-api",
                State = MachineClientState.Active,
            });
        }

        if (!context.Mandates.Any(m => m.Key == MandateKey))
        {
            context.Mandates.Add(new Mandate
            {
                Name = TestHelpers.Localized(nameof(KeycloakOpaqueTokenTest)),
                Key = MandateKey,
                IsPublic = true,
                AllowDelivery = true,
                PipelineId = "ili_validation",
                FileTypes = [".xtf"],
                EvaluateComment = FieldEvaluationType.NotEvaluated,
                EvaluatePartial = FieldEvaluationType.NotEvaluated,
                EvaluatePrecursorDelivery = FieldEvaluationType.NotEvaluated,
            });
        }

        context.SaveChanges();
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
        /// <summary>
        /// Where this host keeps the uploads of the machine delivery. Removed with the host.
        /// </summary>
        public string RootDirectory { get; } = Path.Combine(Path.GetTempPath(), $"geopilot-keycloak-opaque-{Guid.NewGuid():N}");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Auth:AccessTokenFormat", "Opaque");
            builder.UseSetting("Auth:IntrospectionUrl", $"{RealmUrl}/protocol/openid-connect/token/introspect");
            builder.UseSetting("Auth:IntrospectionAuthMethod", "ClientSecretBasic");
            builder.UseSetting("Auth:ConfidentialClientId", ConfidentialClientId);
            builder.UseSetting("Auth:ConfidentialClientSecret", ConfidentialClientSecret);
            builder.UseSetting("Auth:Audience", ApiAudience);
            builder.UseSetting("Auth:UserInfoUrl", $"{RealmUrl}/protocol/openid-connect/userinfo");

            // A host that offers the machine delivery and takes the files with the request, so a client
            // credentials token can be driven through the whole resource. All read while the host is built.
            builder.UseSetting("MachineDelivery:Enabled", "true");
            builder.UseSetting("Upload:Backend", "Direct");
            builder.UseSetting("Upload:Direct:Directory", RootDirectory);

            base.ConfigureWebHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && Directory.Exists(RootDirectory))
                Directory.Delete(RootDirectory, true);
        }
    }
}
