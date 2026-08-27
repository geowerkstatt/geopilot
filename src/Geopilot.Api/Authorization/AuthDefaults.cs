namespace Geopilot.Api.Authorization;

/// <summary>
/// Defines authentication constants shared between the JWT bearer setup and its consumers.
/// </summary>
public static class AuthDefaults
{
    /// <summary>
    /// The cookie the web frontend stores the OIDC token in. Read as a bearer-token fallback by the
    /// JWT setup in Program.cs, and by the execution protocol to classify the kind of client.
    /// </summary>
    public const string AuthCookieName = "geopilot.auth";
}
