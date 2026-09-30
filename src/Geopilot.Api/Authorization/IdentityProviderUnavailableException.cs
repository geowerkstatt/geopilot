namespace Geopilot.Api.Authorization;

/// <summary>
/// The identity provider did not answer a token request, so the token was neither confirmed nor rejected.
/// </summary>
public sealed class IdentityProviderUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityProviderUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the failed request.</param>
    public IdentityProviderUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityProviderUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the failed request.</param>
    /// <param name="innerException">The transport error that caused the failure.</param>
    public IdentityProviderUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
