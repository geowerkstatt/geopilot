namespace Geopilot.Api.Authorization;

/// <summary>
/// Configuration options for requesting a person's user info from the identity provider.
/// </summary>
public class UserInfoOptions
{
    /// <summary>
    /// The configuration section these options are bound to.
    /// </summary>
    public const string SectionName = "Auth";

    /// <summary>
    /// The user info claims a person's name is joined from when <see cref="UserNameClaims"/> is not configured.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultUserNameClaims = ["name"];

    /// <summary>
    /// The URL of the identity provider's user info endpoint.
    /// </summary>
    public string? UserInfoUrl { get; set; }

    /// <summary>
    /// The user info claims a person's name is joined from, separated by a space. Null or empty means <see cref="DefaultUserNameClaims"/>.
    /// </summary>
    // Null rather than the default: the configuration binder appends to a preset list instead of replacing it.
    public IReadOnlyList<string>? UserNameClaims { get; set; }
}
