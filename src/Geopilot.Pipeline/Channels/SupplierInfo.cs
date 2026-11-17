namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Who delivered, as a channel sees it. Personal data when a user delivered, or when users are responsible for
/// the machine client that did.
/// </summary>
internal sealed record SupplierInfo
{
    /// <summary>
    /// Whether a user or a machine client delivered.
    /// </summary>
    public required SupplierKind Kind { get; init; }

    /// <summary>
    /// The full name of the user or the name of the machine client.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The email address of the user, or <see langword="null"/> for a machine client.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// For a machine client, the active users responsible for it, who get the mail meant for the supplier; empty for a
    /// user.
    /// </summary>
    public IReadOnlyList<ResponsibleUserInfo> ResponsibleUsers { get; init; } = [];
}

/// <summary>
/// A user responsible for the machine client that delivered, as a channel sees it. Personal data.
/// </summary>
/// <param name="Name">The full name of the user.</param>
/// <param name="Email">The email address of the user.</param>
internal sealed record ResponsibleUserInfo(string Name, string Email);
