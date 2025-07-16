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
    /// The scopes required to authenticate with the IDP.
    /// </summary>
    public string Scope { get; set; } = string.Empty;
}
