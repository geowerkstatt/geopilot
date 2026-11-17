namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Whether a user or a machine client delivered.
/// </summary>
internal enum SupplierKind
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
