using Asp.Versioning;
using Geopilot.Api.Contracts;
using Geopilot.Api.Exceptions;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Processing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;
using System.Web;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Controller for processing jobs.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("2.0")]
[AllowAnonymous]
public class ProcessingController : ControllerBase
{
    private readonly ILogger<ProcessingController> logger;
    private readonly IProcessingService processingService;
    private readonly IDownloadFileStore downloadFileStore;
    private readonly IVisualizationFileStore visualizationFileStore;
    private readonly IContentTypeProvider contentTypeProvider;
    private readonly Context context;
    private readonly TimeSpan jobRetention;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingController"/> class.
    /// </summary>
    public ProcessingController(ILogger<ProcessingController> logger, IProcessingService processingService, IDownloadFileStore downloadFileStore, IVisualizationFileStore visualizationFileStore, IContentTypeProvider contentTypeProvider, Context context, IOptions<ProcessingOptions> processingOptions)
    {
        ArgumentNullException.ThrowIfNull(processingOptions);
        jobRetention = processingOptions.Value.JobRetention;
        this.logger = logger;
        this.processingService = processingService;
        this.downloadFileStore = downloadFileStore;
        this.visualizationFileStore = visualizationFileStore;
        this.contentTypeProvider = contentTypeProvider;
        this.context = context;
    }

    /// <summary>
    /// Returns the processing settings.
    /// </summary>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "The specified settings for uploading files.", typeof(ProcessingSettingsResponse), "application/json")]
    public async Task<IActionResult> GetProcessingSettings()
    {
        return Ok(new ProcessingSettingsResponse
        {
            AllowedFileExtensions = await processingService.GetSupportedFileExtensionsAsync(),
        });
    }

    /// <summary>
    /// Creates and starts a processing job for the previously uploaded files referenced by the request.
    /// </summary>
    [HttpPost]
    [SwaggerResponse(StatusCodes.Status202Accepted, "The job was created and queued for background preflight and processing.", typeof(ProcessingJobResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request, or the mandate is not valid for the user/upload.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The upload with the specified uploadId cannot be found.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "A job for the upload is still pending or running. Retry once it has ended.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected error while starting the job.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> StartJobAsync([FromBody] StartJobRequest startJobRequest)
    {
        ArgumentNullException.ThrowIfNull(startJobRequest);

        try
        {
            // Open to everyone, so a valid token whose subject nobody knows yet (a person before the web
            // created the user, a machine nobody registered) processes like a caller without a token.
            var supplier = User?.Identity?.IsAuthenticated == true
                        ? await context.GetSupplierOrDefaultAsync(User)
                        : null;

            logger.LogInformation("Starting job for upload <{UploadId}> with mandate <{MandateId}> for <{Supplier}>.", startJobRequest.UploadId, startJobRequest.MandateId, supplier?.ToString() ?? "Unauthenticated");
            var job = await processingService.StartJobAsync(startJobRequest.UploadId, startJobRequest.MandateId, supplier, deliveryFields: null);
            logger.LogInformation("Job with id <{JobId}> is scheduled for execution.", job.Id);

            return AcceptedAtAction(nameof(GetStatus), new { jobId = job.Id }, job.ToResponse(BuildDownloadUrl, BuildVisualizationUrl).WithRunningStepWhileJobRuns());
        }
        catch (ArgumentException ex)
        {
            logger.LogTrace(ex, "Starting job for upload <{UploadId}> failed: upload not found.", startJobRequest.UploadId);
            return Problem(ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogTrace(ex, "Starting job for upload <{UploadId}> failed.", startJobRequest.UploadId);
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (JobAlreadyActiveException ex)
        {
            logger.LogInformation("Starting job for upload <{UploadId}> refused: a job for it is still active.", startJobRequest.UploadId);
            return Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Starting job for upload <{UploadId}> failed unexpectedly.", startJobRequest.UploadId);
            return Problem("An unexpected error occured.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets the status information for the specified <paramref name="jobId"/>.
    /// </summary>
    [HttpGet("{jobId}")]
    [SwaggerResponse(StatusCodes.Status200OK, "The job with the specified jobId was found.", typeof(ProcessingJobResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The job with the specified jobId cannot be found.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> GetStatus(Guid jobId, CancellationToken cancellationToken)
    {
        logger.LogTrace("Status for job <{JobId}> requested.", jobId);

        var run = await context.FindAnswerableRecordingAsync(jobId, jobRetention, cancellationToken);
        if (run == null)
        {
            logger.LogTrace("No job information available for job id <{JobId}>", jobId);
            return Problem($"No job information available for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(run.ToStatusResponse(BuildDownloadUrl, BuildVisualizationUrl).WithRunningStepWhileJobRuns());
    }

    /// <summary>
    /// Download a file produced by the processing pipeline for a job.
    /// </summary>
    [HttpGet("{jobId}/files/{file}", Name = nameof(Download))]
    [SwaggerResponse(StatusCodes.Status200OK, "The specified file was found.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The job or file cannot be found.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> Download(Guid jobId, string file, CancellationToken cancellationToken)
    {
        logger.LogInformation("Download file <{File}> for job <{JobId}> requested.", HttpUtility.HtmlEncode(file), jobId);

        if (!downloadFileStore.Exists(jobId, file))
        {
            logger.LogTrace("No file <{File}> found for job id <{JobId}>", HttpUtility.HtmlEncode(file), jobId);
            return Problem($"No file <{file}> found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
        }

        var stream = downloadFileStore.OpenFile(jobId, file);
        var contentType = contentTypeProvider.GetContentTypeAsString(file);
        var downloadName = await context.FindDownloadNameAsync(jobId, file, cancellationToken) ?? Path.GetFileName(file);
        return File(stream, contentType, downloadName);
    }

    /// <summary>
    /// Fetch a visualization config produced by the processing pipeline for a job.
    /// </summary>
    [HttpGet("{jobId}/visualizations/{file}", Name = nameof(GetVisualization))]
    [SwaggerResponse(StatusCodes.Status200OK, "The specified visualization config was found.", typeof(VisualizationResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The job or visualization config cannot be found.", typeof(ProblemDetails), "application/json")]
    public IActionResult GetVisualization(Guid jobId, string file)
    {
        logger.LogInformation("Visualization config <{File}> for job <{JobId}> requested.", HttpUtility.HtmlEncode(file), jobId);

        if (!visualizationFileStore.Exists(jobId, file))
        {
            logger.LogTrace("No visualization config <{File}> found for job id <{JobId}>", HttpUtility.HtmlEncode(file), jobId);
            return Problem($"No visualization config <{file}> found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
        }

        var stream = visualizationFileStore.OpenFile(jobId, file);
        return File(stream, "application/json");
    }

    private Uri BuildDownloadUrl(Guid jobId, string fileName)
    {
        var url = Url.RouteUrl(nameof(Download), new { jobId, file = fileName }, Request.Scheme, Request.Host.Value)
            ?? throw new InvalidOperationException($"Could not generate download URL for job <{jobId}> file <{fileName}>.");
        return new Uri(url);
    }

    private Uri BuildVisualizationUrl(Guid jobId, string fileName)
    {
        var url = Url.RouteUrl(nameof(GetVisualization), new { jobId, file = fileName }, Request.Scheme, Request.Host.Value)
            ?? throw new InvalidOperationException($"Could not generate visualization URL for job <{jobId}> file <{fileName}>.");
        return new Uri(url);
    }
}
