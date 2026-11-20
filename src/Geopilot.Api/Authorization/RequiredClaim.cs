namespace Geopilot.Api.Authorization;

/// <summary>
/// A claim that the access token, or its introspection response, must have with exactly this value.
/// </summary>
public sealed record RequiredClaim
{
    /// <summary>
    /// Gets the name of the claim, for example <c>tid</c>.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the value the claim must have. The comparison is case-sensitive.
    /// </summary>
    public string Value { get; init; } = string.Empty;
}
