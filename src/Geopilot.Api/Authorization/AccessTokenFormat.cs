namespace Geopilot.Api.Authorization;

/// <summary>
/// Specifies the expected format and validation method for access tokens.
/// </summary>
public enum AccessTokenFormat
{
    /// <summary>
    /// Self-contained JSON Web Token validated locally.
    /// </summary>
    Jwt,

    /// <summary>
    /// Opaque token validated via RFC 7662 introspection at the identity provider.
    /// </summary>
    Opaque,
}
