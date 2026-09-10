using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Authentication handler for opaque access tokens validated via RFC 7662 introspection.
/// </summary>
public class OpaqueTokenHandler : AuthenticationHandler<OpaqueTokenOptions>
{
    /// <summary>
    /// The name of the HTTP client used for introspection requests.
    /// </summary>
    public const string HttpClientName = "OpaqueTokenIntrospection";

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IGeopilotUserInfoService userInfoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpaqueTokenHandler"/> class.
    /// </summary>
    /// <param name="options">The monitor for opaque token options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="userInfoService">The user info service.</param>
    public OpaqueTokenHandler(
        IOptionsMonitor<OpaqueTokenOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHttpClientFactory httpClientFactory,
        IGeopilotUserInfoService userInfoService)
        : base(options, logger, encoder)
    {
        this.httpClientFactory = httpClientFactory;
        this.userInfoService = userInfoService;
    }

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var cookieToken = Request.Cookies[AuthDefaults.AuthCookieName];
        string? token = null;
        if (!string.IsNullOrEmpty(cookieToken))
        {
            token = cookieToken;
        }
        else if (Request.Headers.TryGetValue(HeaderNames.Authorization, out var authHeader))
        {
            var headerValue = authHeader.ToString();
            if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = headerValue["Bearer ".Length..].Trim();
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, Options.IntrospectionUrl);

        var formFields = new List<KeyValuePair<string, string>>
        {
            new("token", token),
            new("token_type_hint", "access_token"),
        };

        switch (Options.IntrospectionAuthMethod)
        {
            case IntrospectionAuthMethod.ClientSecretBasic:
                var id = Uri.EscapeDataString(Options.ConfidentialClientId);
                var secret = Uri.EscapeDataString(Options.ConfidentialClientSecret);
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                break;

            case IntrospectionAuthMethod.ClientSecretPost:
                formFields.Add(new("client_id", Options.ConfidentialClientId));
                formFields.Add(new("client_secret", Options.ConfidentialClientSecret));
                break;

            default:
                throw new InvalidOperationException($"Unsupported introspection authentication method: {Options.IntrospectionAuthMethod}.");
        }

        request.Content = new FormUrlEncodedContent(formFields);

        // shortcut: no cache across requests (two IdP round trips per request), add memory cache keyed by token hash if throughput matters.
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, Context.RequestAborted);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Introspection request failed.");
            return AuthenticateResult.Fail("Introspection request failed.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Introspection request failed with status code {StatusCode}.", response.StatusCode);
                return AuthenticateResult.Fail($"Introspection request failed with status code {response.StatusCode}.");
            }

            JsonDocument doc;
            try
            {
                doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(Context.RequestAborted), cancellationToken: Context.RequestAborted);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse introspection response JSON.");
                return AuthenticateResult.Fail("Failed to parse introspection response JSON.");
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (!root.TryGetProperty("active", out var activeProp) || activeProp.ValueKind != JsonValueKind.True)
                {
                    return AuthenticateResult.Fail("Token is not active.");
                }

                if (!string.IsNullOrWhiteSpace(Options.Audience) && root.TryGetProperty("aud", out var audProp))
                {
                    var audienceMatches = false;
                    if (audProp.ValueKind == JsonValueKind.String)
                    {
                        audienceMatches = string.Equals(audProp.GetString(), Options.Audience, StringComparison.Ordinal);
                    }
                    else if (audProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in audProp.EnumerateArray())
                        {
                            if (element.ValueKind == JsonValueKind.String && string.Equals(element.GetString(), Options.Audience, StringComparison.Ordinal))
                            {
                                audienceMatches = true;
                                break;
                            }
                        }
                    }

                    if (!audienceMatches)
                    {
                        Logger.LogWarning("Introspection response audience does not match configured audience {Audience}.", Options.Audience);
                        return AuthenticateResult.Fail("Introspection response audience does not match configured audience.");
                    }
                }
            }
        }

        var userInfo = await userInfoService.GetUserInfoAsync(token);
        if (userInfo is null)
        {
            Logger.LogWarning("Failed to retrieve user info for opaque token.");
            return AuthenticateResult.Fail("Failed to retrieve user info.");
        }

        var identity = new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, userInfo.Sub)],
            JwtBearerDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    /// <inheritdoc/>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append(HeaderNames.WWWAuthenticate, "Bearer");
        return Task.CompletedTask;
    }
}
