using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace GeoCop.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VersionController : ControllerBase
    {
        [HttpGet]
        public string Get()
        {
            var assembly = typeof(Program).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
                assembly.GetName()?.Version?.ToString() ?? String.Empty;
        }
    }
}
