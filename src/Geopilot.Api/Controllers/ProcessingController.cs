using Api;
using Asp.Versioning;
using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Processing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
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
    private readonly IContentTypeProvider contentTypeProvider;
    private readonly Context context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingController"/> class.
    /// </summary>
    public ProcessingController(ILogger<ProcessingController> logger, IProcessingService processingService, IDownloadFileStore downloadFileStore, IContentTypeProvider contentTypeProvider, Context context)
    {
        this.logger = logger;
        this.processingService = processingService;
        this.downloadFileStore = downloadFileStore;
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
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected error while starting the job.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> StartJobAsync([FromBody] StartJobRequest startJobRequest)
    {
        ArgumentNullException.ThrowIfNull(startJobRequest);

        try
        {
            var user = User?.Identity?.IsAuthenticated == true
                        ? await context.GetUserByPrincipalAsync(User)
                        : null;

            logger.LogInformation("Starting job for upload <{UploadId}> with mandate <{MandateId}> for user <{AuthIdentifier}>.", startJobRequest.UploadId, startJobRequest.MandateId, user?.AuthIdentifier ?? "Unauthenticated");
            var job = await processingService.StartJobAsync(startJobRequest.UploadId, startJobRequest.MandateId, user);
            logger.LogInformation("Job with id <{JobId}> is scheduled for execution.", job.Id);

            return AcceptedAtAction(nameof(GetStatus), new { jobId = job.Id }, job.ToResponse(BuildDownloadUrl));
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
    public IActionResult GetStatus(Guid jobId)
    {
        logger.LogTrace("Status for job <{JobId}> requested.", jobId);

        var job = processingService.GetJob(jobId);
        if (job == null)
        {
            logger.LogTrace("No job information available for job id <{JobId}>", jobId);
            return Problem($"No job information available for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(job.ToResponse(BuildDownloadUrl));
    }

    /// <summary>
    /// Download a file produced by the processing pipeline for a job.
    /// </summary>
    [HttpGet("{jobId}/files/{file}", Name = nameof(Download))]
    [SwaggerResponse(StatusCodes.Status200OK, "The specified file was found.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The job or file cannot be found.", typeof(ProblemDetails), "application/json")]
    public IActionResult Download(Guid jobId, string file)
    {
        logger.LogInformation("Download file <{File}> for job <{JobId}> requested.", HttpUtility.HtmlEncode(file), jobId);

        if (!downloadFileStore.Exists(jobId, file))
        {
            logger.LogTrace("No file <{File}> found for job id <{JobId}>", HttpUtility.HtmlEncode(file), jobId);
            return Problem($"No file <{file}> found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
        }

        var stream = downloadFileStore.OpenFile(jobId, file);
        var contentType = contentTypeProvider.GetContentTypeAsString(file);
        var downloadName = ResolveOriginalFileName(jobId, file) ?? Path.GetFileName(file);
        return File(stream, contentType, downloadName);
    }

    private string? ResolveOriginalFileName(Guid jobId, string persistedFileName)
    {
        // Each step keeps an in-memory mapping from persisted (random) name → original
        // human-readable name. After the job ages out of the store we fall back to the
        // persisted name; by then the temp dirs are usually gone anyway.
        var job = processingService.GetJob(jobId);
        return job?.Pipeline?.Steps
            .SelectMany(s => s.Downloads.Concat(s.DeliveryFiles))
            .FirstOrDefault(f => f.PersistedFileName == persistedFileName)
            ?.OriginalFileName;
    }

    private Uri BuildDownloadUrl(Guid jobId, string fileName)
    {
        var url = Url.RouteUrl(nameof(Download), new { jobId, file = fileName }, Request.Scheme, Request.Host.Value)
            ?? throw new InvalidOperationException($"Could not generate download URL for job <{jobId}> file <{fileName}>.");
        return new Uri(url);
    }
}
