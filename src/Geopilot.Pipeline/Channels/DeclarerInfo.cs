namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Who declared a delivery, as a channel sees it. Personal data when a user declared.
/// </summary>
internal sealed record DeclarerInfo
{
    /// <summary>
    /// Whether a user or a machine client declared the delivery.
    /// </summary>
    public required DeclarerKind Kind { get; init; }

    /// <summary>
    /// The full name of the user or the name of the machine client.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The email address of the user, or <see langword="null"/> for a machine client.
    /// </summary>
    public string? Email { get; init; }
}
