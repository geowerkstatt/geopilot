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
    /// The client id of the client application registered at the authority.
    /// </summary>
    public string ClientAudience { get; set; } = string.Empty;

    /// <summary>
    /// The scope required for a client to authenticate with the IDP.
    /// </summary>
    public string FullScope { get; set; } = string.Empty;
}
