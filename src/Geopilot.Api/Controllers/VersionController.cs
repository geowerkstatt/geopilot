using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Reflection;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Constroller for Version information.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[AllowAnonymous]
public class VersionController : ControllerBase
{
    /// <summary>
    /// Gets the application version.
    /// </summary>
    /// <returns>Version string.</returns>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the application version.", typeof(string), "text/plain")]
    public string Get()
    {
        return GetVersion();
    }

    /// <summary>
    /// Gets the application version including the commit hash.
    /// </summary>
    public static string GetVersion()
    {
        var assembly = typeof(Program).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            assembly.GetName()?.Version?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Gets the application version without the commit hash.
    /// </summary>
    public static string GetShortVersion()
    {
        return GetVersion().Split('+')[0];
    }
}
