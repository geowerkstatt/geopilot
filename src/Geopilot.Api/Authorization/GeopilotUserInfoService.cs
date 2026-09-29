using Geopilot.Api.Contracts;
using System.Text.Json;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Service for retrieving user information from the identity provider.
/// </summary>
public class GeopilotUserInfoService : IGeopilotUserInfoService
{
    /// <summary>
    /// The name of the configured HTTP client for user info requests.
    /// </summary>
    public const string HttpClientName = "GeopilotUserInfo";

    private static readonly string[] DefaultUserNameClaims = ["name"];

    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<GeopilotUserInfoService> logger;
    private readonly IReadOnlyList<string> userNameClaims;

    // Invariant: single-slot cache requires Scoped service lifetime.
    private string? cachedToken;
    private UserInfoResponse? cachedUserInfo;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopilotUserInfoService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger for user info service related logging.</param>
    public GeopilotUserInfoService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GeopilotUserInfoService> logger)
        : this((httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory))).CreateClient(HttpClientName), configuration ?? throw new ArgumentNullException(nameof(configuration)), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopilotUserInfoService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for making requests to the identity provider.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger for user info service related logging.</param>
    internal GeopilotUserInfoService(HttpClient httpClient, IConfiguration configuration, ILogger<GeopilotUserInfoService> logger)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;

        var configuredUserNameClaims = configuration.GetSection("Auth:UserNameClaims").Get<string[]>();
        userNameClaims = configuredUserNameClaims is { Length: > 0 } ? configuredUserNameClaims : DefaultUserNameClaims;
    }

    /// <inheritdoc/>
    public async Task<UserInfoResponse?> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (accessToken == cachedToken && cachedUserInfo is not null)
        {
            return cachedUserInfo;
        }

        try
        {
            var userInfoEndpoint = configuration["Auth:UserInfoUrl"];
            using var request = new HttpRequestMessage(HttpMethod.Get, userInfoEndpoint);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var response = await httpClient.SendAsync(request, cancellationToken);
            if ((int)response.StatusCode >= 500)
            {
                throw new IdentityProviderUnavailableException($"User info request failed with status code {response.StatusCode}.");
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to retrieve user info. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            using var document = JsonDocument.Parse(content);
            var userInfo = document.RootElement.Deserialize<UserInfoResponse>(JsonOptions);
            if (userInfo is not null)
            {
                userInfo.Name = ComposeUserName(document.RootElement);
            }

            if (string.IsNullOrEmpty(userInfo?.Sub) || string.IsNullOrEmpty(userInfo?.Email) ||
                string.IsNullOrEmpty(userInfo?.Name))
            {
                logger.LogError(
                    "UserInfo response missing required fields: sub, email and a name from the claims <{UserNameClaims}> (Auth:UserNameClaims).",
                    string.Join(", ", userNameClaims));
                return null;
            }

            cachedToken = accessToken;
            cachedUserInfo = userInfo;
            return userInfo;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new IdentityProviderUnavailableException("User info request failed.", ex);
        }
        catch (Exception ex) when (ex is not IdentityProviderUnavailableException)
        {
            logger.LogError(ex, "Error retrieving user info.");
            return null;
        }
    }

    // Joins the configured claims that carry a value, so a person without a surname still gets a name.
    // Claim names match case-insensitively, like the rest of the user info response.
    private string ComposeUserName(JsonElement userInfo)
    {
        var claims = userInfo.EnumerateObject().ToList();
        var parts = userNameClaims
            .Select(claimName => claims.FirstOrDefault(claim => string.Equals(claim.Name, claimName, StringComparison.OrdinalIgnoreCase)).Value)
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()?.Trim())
            .Where(part => !string.IsNullOrEmpty(part));
        return string.Join(' ', parts);
    }
}
