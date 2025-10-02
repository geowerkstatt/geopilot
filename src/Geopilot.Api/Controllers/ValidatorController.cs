using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Geopilot.Api.Controllers
{
    /// <summary>
    /// Controller for information about available validators and configurations.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    public class ValidatorController : ControllerBase
    {
        private readonly IEnumerable<IValidator> validators;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidatorController"/> class.
        /// </summary>
        /// <param name="validators">List of all IValidator implementations.</param>
        public ValidatorController(IEnumerable<IValidator> validators)
        {
            this.validators = validators;
        }

        /// <summary>
        /// Gets all available validators and their supported configuraitions.
        /// </summary>
        [HttpGet]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns a list of available validators and their supported configurations.")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "There was an error while reading the profiles from a check service")]
        public async Task<IActionResult> Get()
        {
            var result = new Dictionary<string, ValidatorConfiguration>();

            foreach (var validator in validators)
            {
                var supportedExtensions = await validator.GetSupportedFileExtensionsAsync();
                result[validator.Name] = new ValidatorConfiguration
                {
                    SupportedFileExtensions = supportedExtensions.ToList(),
                    Profiles = await validator.GetSupportedProfilesAsync(),
                };
            }

            return Ok(result);
        }
    }
}
