namespace Geopilot.Api.Models;

/// <summary>
/// The status of a user account.
/// </summary>
public enum UserState
{
    /// <summary>
    /// The user account is inactive and cannot access the system.
    /// </summary>
    Inactive = 0,

    /// <summary>
    /// The user account is active and can access the system.
    /// </summary>
    Active = 1,
}
