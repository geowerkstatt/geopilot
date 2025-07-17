using Geopilot.Api.Contracts;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Service for retrieving user information from the identity provider.
/// </summary>
public interface IGeopilotUserInfoService
{
    /// <summary>
    /// Retrieves user information from the identity provider using the provided access token.
    /// </summary>
    /// <param name="accessToken">The access token to authenticate with the identity provider.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the user information or null if the operation failed.</returns>
    Task<UserInfoResponse?> GetUserInfoAsync(string accessToken);
}
