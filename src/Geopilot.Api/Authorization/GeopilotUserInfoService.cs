using Geopilot.Api.Contracts;
using System.Text.Json;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Service for retrieving user information from the identity provider.
/// </summary>
public class GeopilotUserInfoService : IGeopilotUserInfoService
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<GeopilotUserInfoService> logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GeopilotUserInfoService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for making requests to the identity provider.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger for user info service related logging.</param>
    public GeopilotUserInfoService(HttpClient httpClient, IConfiguration configuration, ILogger<GeopilotUserInfoService> logger)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UserInfoResponse?> GetUserInfoAsync(string accessToken)
    {
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

            return userInfo;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving user info.");
            return null;
        }
    }
}
