namespace Geopilot.Api.Authorization;

/// <summary>
/// Defines a claim and exact value that every access token (or introspection response) must contain.
/// </summary>
public sealed record RequiredClaim
{
    /// <summary>
    /// Gets the claim name, such as <c>tid</c>.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exact case-sensitive value that the claim must contain.
    /// </summary>
    public string Value { get; init; } = string.Empty;
}
