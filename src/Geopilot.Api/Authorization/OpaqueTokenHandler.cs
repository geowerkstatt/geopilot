using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
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
    private readonly IGeopilotUserResolver userResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpaqueTokenHandler"/> class.
    /// </summary>
    /// <param name="options">The monitor for opaque token options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="userResolver">Prefetches the user info of a person's token.</param>
    public OpaqueTokenHandler(
        IOptionsMonitor<OpaqueTokenOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHttpClientFactory httpClientFactory,
        IGeopilotUserResolver userResolver)
        : base(options, logger, encoder)
    {
        this.httpClientFactory = httpClientFactory;
        this.userResolver = userResolver;
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
                var id = WebUtility.UrlEncode(Options.ConfidentialClientId);
                var secret = WebUtility.UrlEncode(Options.ConfidentialClientSecret);
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                break;

            case IntrospectionAuthMethod.ClientSecretPost:
                formFields.Add(new("client_id", Options.ConfidentialClientId));
                formFields.Add(new("client_secret", Options.ConfidentialClientSecret));
                break;

            default:
                throw new UnreachableException($"Unsupported introspection authentication method: {Options.IntrospectionAuthMethod}.");
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
            return AuthenticateResult.Fail(new IdentityProviderUnavailableException("Introspection request failed.", ex));
        }

        string? subject = null;
        using (response)
        {
            if ((int)response.StatusCode >= 500)
            {
                Logger.LogWarning("Introspection request failed with status code {StatusCode}.", response.StatusCode);
                return AuthenticateResult.Fail(new IdentityProviderUnavailableException($"Introspection request failed with status code {response.StatusCode}."));
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                Logger.LogWarning("Introspection credentials were rejected with status code {StatusCode}.", response.StatusCode);
                return AuthenticateResult.Fail($"Introspection request failed with status code {response.StatusCode}.");
            }

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
                return AuthenticateResult.Fail(new IdentityProviderUnavailableException("Failed to parse introspection response JSON.", ex));
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (!root.TryGetProperty("active", out var activeProp) || activeProp.ValueKind != JsonValueKind.True)
                {
                    return AuthenticateResult.Fail("Token is not active.");
                }

                if (!string.IsNullOrWhiteSpace(Options.Audience))
                {
                    if (!root.TryGetProperty("aud", out var audProp))
                    {
                        Logger.LogWarning("Introspection response contains no aud, but audience {Audience} is configured.", Options.Audience);
                        return AuthenticateResult.Fail("Introspection response contains no audience.");
                    }

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

                subject = ReadString(root, "sub") ?? ReadString(root, "client_id");
            }
        }

        // The subject is what the authorization handlers look up, for a person as much as for a machine client,
        // so it comes from the introspection itself: user info describes a person, and a token issued for client
        // credentials has none. RFC 7662 leaves both fields optional; a provider that names no sub for a machine
        // still names the client it issued the token to.
        if (string.IsNullOrWhiteSpace(subject))
        {
            Logger.LogWarning("Introspection response names neither sub nor client_id.");
            return AuthenticateResult.Fail("Introspection response contains no subject.");
        }

        // Requested for the same reason as on the JWT path: an identity provider that does not answer fails the
        // authentication with a typed reason and gets a 503, instead of a 403 from the authorization handler. The
        // response decides nothing here; the resolver keeps it for the authorization handlers and asks nothing
        // for a registered machine client.
        try
        {
            await userResolver.PrefetchUserInfoAsync(subject, token, Context.RequestAborted);
        }
        catch (IdentityProviderUnavailableException ex)
        {
            Logger.LogWarning(ex, "User info request failed.");
            return AuthenticateResult.Fail(ex);
        }

        var identity = new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, subject)],
            JwtBearerDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    /// <inheritdoc/>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var authResult = await HandleAuthenticateOnceSafeAsync();
        if (authResult.Failure is IdentityProviderUnavailableException)
        {
            await Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Authentication unavailable",
                detail: "Authentication currently not possible.")
                .ExecuteAsync(Context);
            return;
        }

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append(HeaderNames.WWWAuthenticate, "Bearer");
    }

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
