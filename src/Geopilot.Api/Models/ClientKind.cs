namespace Geopilot.Api.Models;

/// <summary>
/// The way a processing job was started, classified from the request. Persisted as text. It says nothing about
/// who started the job: that is the user or the machine client on the run, and a person calling the API from
/// Swagger or a script is as much an <see cref="ApiClient"/> as a registered machine is.
/// </summary>
public enum ClientKind
{
    /// <summary>The classification was not possible.</summary>
    Unknown,

    /// <summary>The geopilot web frontend (the token came from the geopilot.auth cookie, or the request carried browser fetch metadata).</summary>
    WebClient,

    /// <summary>A direct call of the API, by a person or a machine (the token came from the Authorization header).</summary>
    ApiClient,
}
