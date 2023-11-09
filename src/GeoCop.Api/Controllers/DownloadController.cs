using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Swashbuckle.AspNetCore.Annotations;

namespace GeoCop.Api.Controllers
{
    /// <summary>
    /// Controller to download log files of validation jobs.
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class DownloadController : ControllerBase
    {
        private readonly ILogger<DownloadController> logger;
        private readonly IValidationService validationService;
        private readonly IFileProvider fileProvider;
        private readonly IContentTypeProvider contentTypeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadController"/> class.
        /// </summary>
        public DownloadController(ILogger<DownloadController> logger, IValidationService validationService, IFileProvider fileProvider, IContentTypeProvider contentTypeProvider)
        {
            this.logger = logger;
            this.validationService = validationService;
            this.fileProvider = fileProvider;
            this.contentTypeProvider = contentTypeProvider;
        }

        /// <summary>
        /// Gets the status information for the specified <paramref name="jobId"/>.
        /// </summary>
        /// <param name="jobId" example="2e71ae96-e6ad-4b67-b817-f09412d09a2c">The job identifier.</param>
        /// <param name="file">The file name.</param>
        /// <returns>The status information for the specified <paramref name="jobId"/>.</returns>
        [HttpGet("{jobId}/{file}")]
        [SwaggerResponse(StatusCodes.Status200OK, "The specified log file was found.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), new[] { "application/json" })]
        [SwaggerResponse(StatusCodes.Status404NotFound, "The job or log file cannot be found.", typeof(ProblemDetails), new[] { "application/json" })]
        public IActionResult Download(Guid jobId, string file)
        {
            logger.LogTrace("Download file <{File}> for job <{JobId}> requested.", file, jobId);

            fileProvider.Initialize(jobId);

            var jobStatus = validationService.GetJobStatus(jobId);
            if (jobStatus == null)
            {
                return Problem($"No job information available for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
            }

            if (!fileProvider.Exists(file))
            {
                return Problem($"No log file <{file}> found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
            }

            var logFile = fileProvider.Open(file);
            var contentType = contentTypeProvider.TryGetContentType(file, out var type) ? type : "application/octet-stream";
            return File(logFile, contentType);
        }
    }
}
