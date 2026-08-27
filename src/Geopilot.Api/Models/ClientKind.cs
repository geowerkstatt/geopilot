namespace Geopilot.Api.Models;

/// <summary>
/// The kind of client that started a processing job, classified from the request. Persisted as text.
/// </summary>
public enum ClientKind
{
    /// <summary>The classification was not possible.</summary>
    Unknown,

    /// <summary>The geopilot web frontend (the token came from the geopilot.auth cookie, or the request carried browser fetch metadata).</summary>
    WebClient,

    /// <summary>A machine client calling the API directly (the token came from the Authorization header).</summary>
    ApiClient,
}
