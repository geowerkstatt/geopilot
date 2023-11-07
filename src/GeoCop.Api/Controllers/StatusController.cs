using Asp.Versioning;
using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace GeoCop.Api.Controllers
{
    /// <summary>
    /// Controller to get the status information of validation jobs.
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class StatusController : Controller
    {
        private readonly ILogger<StatusController> logger;
        private readonly IValidatorService validatorService;
        private readonly IFileProvider fileProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatusController"/> class.
        /// </summary>
        public StatusController(ILogger<StatusController> logger, IValidatorService validatorService, IFileProvider fileProvider)
        {
            this.logger = logger;
            this.validatorService = validatorService;
            this.fileProvider = fileProvider;
        }

        /// <summary>
        /// Gets the status information for the specified <paramref name="jobId"/>.
        /// </summary>
        /// <param name="version">The API version.</param>
        /// <param name="jobId" example="2e71ae96-e6ad-4b67-b817-f09412d09a2c">The job identifier.</param>
        /// <returns>The status information for the specified <paramref name="jobId"/>.</returns>
        [HttpGet("{jobId}")]
        [SwaggerResponse(StatusCodes.Status200OK, "The job with the specified jobId was found.", typeof(StatusResponse), new[] { "application/json" })]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), new[] { "application/json" })]
        [SwaggerResponse(StatusCodes.Status404NotFound, "The job with the specified jobId cannot be found.", typeof(ProblemDetails), new[] { "application/json" })]
        public IActionResult GetStatus(ApiVersion version, Guid jobId)
        {
            logger.LogTrace("Status for job <{JobId}> requested.", jobId);

            fileProvider.Initialize(jobId);

            var job = validatorService.GetJobStatusOrDefault(jobId);
            if (job == null)
            {
                return Problem($"No job information available for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
            }

            var logFiles = job.LogFiles.ToDictionary(pair => pair.Key, pair => $"/api/v{version.MajorVersion}/download/{jobId}/{pair.Value}");

            return Ok(new StatusResponse
            {
                JobId = jobId,
                Status = job.Status,
                StatusMessage = job.StatusMessage,
                LogFiles = logFiles,
            });
        }
    }
}
