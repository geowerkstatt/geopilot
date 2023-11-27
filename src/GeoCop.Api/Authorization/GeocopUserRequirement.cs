using Microsoft.AspNetCore.Authorization;

namespace GeoCop.Api.Authorization;

/// <summary>
/// Authorization requirement for geocop users.
/// </summary>
public class GeocopUserRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Only allow administrators.
    /// </summary>
    public bool RequireAdmin { get; init; }
}
