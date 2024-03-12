namespace Geopilot.Api.Contracts;

/// <summary>
/// Represents the configured auth options of type BrowserAuthOptions from @azure/msal-browser.
/// </summary>
public class BrowserAuthOptions
{
    /// <summary>
    /// The authority used for authentication.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// The client id of the application registered at the authority.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth redirect URI.
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// Redirect URI after logout.
    /// </summary>
    public string? PostLogoutRedirectUri { get; set; }

    /// <summary>
    /// If true, navigate to the original request URL after login.
    /// </summary>
    public bool? NavigateToLoginRequestUrl { get; set; }
}
