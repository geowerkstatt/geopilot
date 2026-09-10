using Geopilot.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Geopilot.Api.Authorization;

internal sealed class OpaqueTestApp : GeopilotTestApp
{
    public const string OpaqueAdminToken = "valid-admin-opaque-token";
    public const string OpaqueUserToken = "valid-user-opaque-token";
    public const string OpaqueInactiveToken = "inactive-opaque-token";
    public const string Audience = "geopilot-api";
    public const string IntrospectionUrl = "https://idp.geopilot.test/oauth2/introspect";
    public const string UserInfoUrl = "https://idp.geopilot.test/oauth2/userinfo";
    public const string ClientId = "geopilot-api-client";
    public const string ClientSecret = "confidential-secret-value";

    private int userInfoHttpCallCount;

    public int UserInfoHttpCallCount => userInfoHttpCallCount;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Auth:AccessTokenFormat", "Opaque");
        builder.UseSetting("Auth:IntrospectionUrl", IntrospectionUrl);
        builder.UseSetting("Auth:IntrospectionAuthMethod", "ClientSecretBasic");
        builder.UseSetting("Auth:ConfidentialClientId", ClientId);
        builder.UseSetting("Auth:ConfidentialClientSecret", ClientSecret);
        builder.UseSetting("Auth:Audience", Audience);
        builder.UseSetting("Auth:UserInfoUrl", UserInfoUrl);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.ConfigureHttpClientDefaults(clientBuilder =>
            {
                clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new IdpStubHandler(this));
            });
        });
    }

    private sealed class IdpStubHandler : HttpMessageHandler
    {
        private readonly OpaqueTestApp app;

        public IdpStubHandler(OpaqueTestApp app)
        {
            this.app = app;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestUrl = request.RequestUri?.ToString() ?? string.Empty;

            if (requestUrl.StartsWith(IntrospectionUrl, StringComparison.OrdinalIgnoreCase))
            {
                return await HandleIntrospectionAsync(request, cancellationToken);
            }

            if (requestUrl.StartsWith(UserInfoUrl, StringComparison.OrdinalIgnoreCase))
            {
                return HandleUserInfo(request);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static async Task<HttpResponseMessage> HandleIntrospectionAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var authHeader = request.Headers.Authorization;
            var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Uri.EscapeDataString(ClientId)}:{Uri.EscapeDataString(ClientSecret)}"));
            if (authHeader?.Scheme != "Basic" || authHeader.Parameter != expectedCredentials)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            if (request.Content is null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var formContent = await request.Content.ReadAsStringAsync(cancellationToken);
            var parts = formContent.Split('&', StringSplitOptions.RemoveEmptyEntries);
            string? token = null;
            foreach (var part in parts)
            {
                var kvp = part.Split('=', 2);
                if (kvp.Length == 2 && Uri.UnescapeDataString(kvp[0]) == "token")
                {
                    token = Uri.UnescapeDataString(kvp[1]);
                    break;
                }
            }

            string responseJson;
            if (token == OpaqueAdminToken)
            {
                responseJson = JsonSerializer.Serialize(new
                {
                    active = true,
                    aud = Audience,
                    sub = JwtTestTokenBuilder.AdminSub,
                });
            }
            else if (token == OpaqueUserToken)
            {
                responseJson = JsonSerializer.Serialize(new
                {
                    active = true,
                    aud = Audience,
                    sub = JwtTestTokenBuilder.UserSub,
                });
            }
            else
            {
                responseJson = JsonSerializer.Serialize(new
                {
                    active = false,
                });
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }

        private HttpResponseMessage HandleUserInfo(HttpRequestMessage request)
        {
            Interlocked.Increment(ref app.userInfoHttpCallCount);

            var authHeader = request.Headers.Authorization;
            var token = authHeader?.Parameter;

            if (token == OpaqueAdminToken)
            {
                var responseJson = JsonSerializer.Serialize(new
                {
                    sub = JwtTestTokenBuilder.AdminSub,
                    email = "admin@geopilot.ch",
                    name = "Andreas Admin",
                });

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
                };
            }

            if (token == OpaqueUserToken)
            {
                var responseJson = JsonSerializer.Serialize(new
                {
                    sub = JwtTestTokenBuilder.UserSub,
                    email = "user@geopilot.ch",
                    name = "Ursula User",
                });

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }
    }
}
