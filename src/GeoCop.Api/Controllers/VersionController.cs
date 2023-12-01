using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace GeoCop.Api.Controllers;

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
    public string Get()
    {
        var assembly = typeof(Program).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            assembly.GetName()?.Version?.ToString() ?? string.Empty;
    }
}
