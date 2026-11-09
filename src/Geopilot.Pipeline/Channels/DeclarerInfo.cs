namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Who declared a delivery, as a channel sees it. Personal data when a user declared, or when users are responsible for
/// the machine client that did.
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

    /// <summary>
    /// For a machine client, the active users responsible for it, who get the mail meant for the declarer; empty for a
    /// user.
    /// </summary>
    public IReadOnlyList<ResponsibleUserInfo> ResponsibleUsers { get; init; } = [];
}

/// <summary>
/// A user responsible for the machine client that declared a delivery, as a channel sees it. Personal data.
/// </summary>
/// <param name="Name">The full name of the user.</param>
/// <param name="Email">The email address of the user.</param>
internal sealed record ResponsibleUserInfo(string Name, string Email);
