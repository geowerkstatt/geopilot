using Microsoft.AspNetCore.Authorization;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Authorization requirement for geopilot users.
/// </summary>
public class GeopilotUserRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Only allow administrators.
    /// </summary>
    public bool RequireAdmin { get; init; }

    /// <summary>
    /// Only allow active users.
    /// </summary>
    public bool RequireActiveUser { get; init; }
}
