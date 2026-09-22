namespace Geopilot.Api.Models;

/// <summary>
/// The status of a machine client. Its own enum rather than <see cref="UserState"/>, because a client is not
/// a user account and the two must be free to grow apart.
/// </summary>
public enum MachineClientState
{
    /// <summary>
    /// The client is inactive: its token no longer identifies it, so the delivery refuses it and everywhere
    /// else it counts as nobody. Its registration and its deliveries stay.
    /// </summary>
    Inactive = 0,

    /// <summary>
    /// The client is active and may deliver to the mandates of its organisations.
    /// </summary>
    Active = 1,
}
