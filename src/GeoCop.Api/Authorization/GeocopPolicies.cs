namespace GeoCop.Api.Authorization;

/// <summary>
/// Defines constants for the authorization policies.
/// </summary>
public static class GeocopPolicies
{
    /// <summary>
    /// Require admin permissions.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Allow access for all users that are stored in the database.
    /// </summary>
    public const string User = "User";
}
