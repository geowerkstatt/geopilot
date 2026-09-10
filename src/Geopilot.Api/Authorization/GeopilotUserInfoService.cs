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

    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<GeopilotUserInfoService> logger;

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
        : this((httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory))).CreateClient(HttpClientName), configuration, logger)
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
    }

    /// <inheritdoc/>
    public async Task<UserInfoResponse?> GetUserInfoAsync(string accessToken)
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
            var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to retrieve user info. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();

            var userInfo = JsonSerializer.Deserialize<UserInfoResponse>(content, JsonOptions);
            if (string.IsNullOrEmpty(userInfo?.Sub) || string.IsNullOrEmpty(userInfo?.Email) ||
                string.IsNullOrEmpty(userInfo?.Name))
            {
                logger.LogError("UserInfo response missing required fields.");
                return null;
            }

            cachedToken = accessToken;
            cachedUserInfo = userInfo;
            return userInfo;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving user info.");
            return null;
        }
    }
}
