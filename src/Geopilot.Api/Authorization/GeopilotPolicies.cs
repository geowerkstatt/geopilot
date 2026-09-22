namespace Geopilot.Api.Authorization;

/// <summary>
/// Defines constants for the authorization policies.
/// </summary>
public static class GeopilotPolicies
{
    /// <summary>
    /// Require admin permissions.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Allow access for all users that are stored in the database.
    /// </summary>
    public const string User = "User";

    /// <summary>
    /// Allow access for whoever may declare a delivery: a stored user or a registered machine client. The
    /// only policy a machine client satisfies, and therefore the boundary of the machine delivery surface.
    /// </summary>
    public const string Declarer = "Declarer";
}
