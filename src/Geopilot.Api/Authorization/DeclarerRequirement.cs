using Microsoft.AspNetCore.Authorization;

namespace Geopilot.Api.Authorization;

/// <summary>
/// Authorization requirement for the machine delivery: the caller is an active user or an active, registered
/// machine client. The one requirement in the application a machine client can satisfy.
/// </summary>
public class DeclarerRequirement : IAuthorizationRequirement
{
}
