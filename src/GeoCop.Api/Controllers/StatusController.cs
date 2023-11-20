using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace GeoCop.Api.Controllers
{
    /// <summary>
    /// Controller to get the status information of validation jobs.
    /// </summary>
    [AllowAnonymous]
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class StatusController : Controller
    {
        private readonly ILogger<StatusController> logger;
        private readonly IValidationService validationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatusController"/> class.
        /// </summary>
        public StatusController(ILogger<StatusController> logger, IValidationService validationService)
        {
            this.logger = logger;
            this.validationService = validationService;
        }

        /// <summary>
        /// Gets the status information for the specified <paramref name="jobId"/>.
        /// </summary>
        /// <param name="jobId" example="2e71ae96-e6ad-4b67-b817-f09412d09a2c">The job identifier.</param>
        /// <returns>The status information for the specified <paramref name="jobId"/>.</returns>
        [HttpGet("{jobId}")]
        [SwaggerResponse(StatusCodes.Status200OK, "The job with the specified jobId was found.", typeof(ValidationJobStatus), new[] { "application/json" })]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), new[] { "application/json" })]
        [SwaggerResponse(StatusCodes.Status404NotFound, "The job with the specified jobId cannot be found.", typeof(ProblemDetails), new[] { "application/json" })]
        public IActionResult GetStatus(Guid jobId)
        {
            logger.LogTrace("Status for job <{JobId}> requested.", jobId);

            var jobStatus = validationService.GetJobStatus(jobId);
            if (jobStatus == null)
            {
                logger.LogTrace("No job information available for job id <{JobId}>", jobId);
                return Problem($"No job information available for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
            }

            return Ok(jobStatus);
        }
    }
}
