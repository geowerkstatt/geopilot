using Microsoft.AspNetCore.Authentication;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Options for opaque token authentication via RFC 7662 introspection.
/// </summary>
public class OpaqueTokenOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Gets or sets the URL of the OAuth 2.0 introspection endpoint.
    /// </summary>
    public string IntrospectionUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client id of the API registered at the identity provider.
    /// </summary>
    public string ConfidentialClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret for introspection endpoint authentication.
    /// </summary>
    public string ConfidentialClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client authentication method for the introspection endpoint.
    /// </summary>
    public IntrospectionAuthMethod? IntrospectionAuthMethod { get; set; }

    /// <summary>
    /// Gets or sets the expected audience of the token.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether HTTPS is required for the introspection endpoint.
    /// Derived from the environment on startup (false in Development, true otherwise).
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <inheritdoc/>
    public override void Validate()
    {
        base.Validate();

        if (string.IsNullOrWhiteSpace(IntrospectionUrl))
        {
            throw new InvalidOperationException("Auth:IntrospectionUrl is required.");
        }

        if (RequireHttpsMetadata && !IntrospectionUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Auth:IntrospectionUrl must use HTTPS when RequireHttpsMetadata is enabled.");
        }

        if (string.IsNullOrWhiteSpace(ConfidentialClientId))
        {
            throw new InvalidOperationException("Auth:ConfidentialClientId is required.");
        }

        if (IntrospectionAuthMethod is null)
        {
            throw new InvalidOperationException("Auth:IntrospectionAuthMethod is required.");
        }

        if (string.IsNullOrWhiteSpace(ConfidentialClientSecret))
        {
            throw new InvalidOperationException("Auth:ConfidentialClientSecret is required.");
        }
    }
}
