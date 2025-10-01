namespace Geopilot.Api.Contracts;

/// <summary>
/// Response model for OIDC userinfo endpoint.
/// </summary>
public class UserInfoResponse
{
    /// <summary>
    /// Subject identifier for the user.
    /// </summary>
    public string Sub { get; set; } = string.Empty;

    /// <summary>
    /// Email address of the user.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Full name of the user.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
