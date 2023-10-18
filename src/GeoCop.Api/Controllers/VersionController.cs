using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace GeoCop.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    internal class VersionController : ControllerBase
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
}
