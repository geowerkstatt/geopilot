namespace Geopilot.Api.Authorization;

/// <summary>
/// Specifies the client authentication method used at the introspection endpoint.
/// </summary>
public enum IntrospectionAuthMethod
{
    /// <summary>
    /// HTTP Basic authentication with client id and secret in the Authorization header.
    /// </summary>
    ClientSecretBasic,

    /// <summary>
    /// Client id and secret included in the request body.
    /// </summary>
    ClientSecretPost,
}
