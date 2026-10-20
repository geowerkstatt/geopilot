namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Whether a user or a machine client declared a delivery.
/// </summary>
internal enum DeclarerKind
{
    /// <summary>
    /// A person with a user account.
    /// </summary>
    User,

    /// <summary>
    /// A registered machine client, delivering without the web interface.
    /// </summary>
    MachineClient,
}
