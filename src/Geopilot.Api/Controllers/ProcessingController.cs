using Api;
using Asp.Versioning;
using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Processing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Swashbuckle.AspNetCore.Annotations;
using System.Diagnostics.CodeAnalysis;
using System.Web;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Controller for processing jobs.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[AllowAnonymous]
public class ProcessingController : ControllerBase
{
    private readonly ILogger<ProcessingController> logger;
    private readonly IProcessingService processingService;
    private readonly IFileProvider fileProvider;
    private readonly IContentTypeProvider contentTypeProvider;
    private readonly Context context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingController"/> class.
    /// </summary>
    public ProcessingController(ILogger<ProcessingController> logger, IProcessingService processingService, IFileProvider fileProvider, IContentTypeProvider contentTypeProvider, Context context)
    {
        this.logger = logger;
        this.processingService = processingService;
        this.fileProvider = fileProvider;
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
    /// Schedules a new job for the given <paramref name="file"/>.
    /// </summary>
    /// <param name="version">The API version.</param>
    /// <param name="file">The file to process.</param>
    /// <remarks>
    /// ## Usage
    ///
    /// ### CURL
    ///
    /// ```bash
    /// curl -i -X POST -H "Content-Type: multipart/form-data" \
    ///   -F 'file=@example.xtf' https://example.com/api/v1/processing
    /// ```
    /// </remarks>
    [HttpPost]
    [SwaggerResponse(StatusCodes.Status201Created, "The processing job was successfully created and is now scheduled for execution.", typeof(ProcessingJobResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status413PayloadTooLarge, "The file is too large. Max allowed request body size is 200 MB.")]
    [SwaggerResponse(
        StatusCodes.Status500InternalServerError,
        "The server encountered an unexpected condition that prevented it from fulfilling the request. Likely the file could not be written or the file extension is not supported.",
        typeof(ProblemDetails),
        "application/json")]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1629:DocumentationTextMustEndWithAPeriod", Justification = "Not applicable for code examples.")]
    public async Task<IActionResult> UploadAsync(ApiVersion version, IFormFile file)
    {
        ArgumentNullException.ThrowIfNull(version);

        logger.LogInformation("File upload started.");
        if (file == null)
        {
            logger.LogTrace("Uploaded file was emtpy.");
            return Problem($"Form data <{nameof(file)}> cannot be empty.", statusCode: StatusCodes.Status400BadRequest);
        }

        var fileExtension = Path.GetExtension(file.FileName);
        if (!await processingService.IsFileExtensionSupportedAsync(fileExtension))
        {
            logger.LogTrace("File extension <{FileExtension}> is not supported.", fileExtension);
            return Problem($"File extension <{fileExtension}> is not supported.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var job = processingService.CreateJob();
            var fileHandle = processingService.CreateFileHandleForJob(job.Id, file.FileName);

            using (fileHandle)
            {
                logger.LogInformation("Start uploading <{FormFile}> as <{File}>, file size: {FileSize}", file.FileName, fileHandle.FileName, file.Length);
                await file.CopyToAsync(fileHandle.Stream).ConfigureAwait(false);
                job = processingService.AddFileToJob(job.Id, file.FileName, fileHandle.FileName);
                logger.LogInformation("Successfully received file: {File}", fileHandle.FileName);
            }

            return CreatedAtAction(nameof(GetStatus), new { jobId = job.Id }, job.ToResponse(BuildDownloadUrl));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "File upload failed.");
            return Problem("An unexpected error occured.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Starts the job with the specified <paramref name="jobId"/> using the pipeline associated with the mandate.
    /// </summary>
    [HttpPatch("{jobId}")]
    [SwaggerResponse(StatusCodes.Status200OK, "The processing job was successfully started.", typeof(ProcessingJobResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status202Accepted, "The cloud upload preflight has been queued for background processing.", typeof(ProcessingJobResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request, or the mandate is not valid for the user/job.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The job with the specified jobId cannot be found.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected error while starting the job.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> StartJobAsync(Guid jobId, [FromBody] StartJobRequest startJobRequest)
    {
        ArgumentNullException.ThrowIfNull(startJobRequest);

        var existing = processingService.GetJob(jobId);
        if (existing == null)
        {
            logger.LogTrace("No job information available for job id <{JobId}>", jobId);
            return Problem($"No job information available for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            var user = User?.Identity?.IsAuthenticated == true
                        ? await context.GetUserByPrincipalAsync(User)
                        : null;

            logger.LogInformation("Starting job <{JobId}> with mandate <{MandateId}> for user <{AuthIdentifier}>.", jobId, startJobRequest.MandateId, user?.AuthIdentifier ?? "Unauthenticated");
            var job = await processingService.StartJobAsync(jobId, startJobRequest.MandateId, user);
            logger.LogInformation("Job with id <{JobId}> is scheduled for execution.", job.Id);

            if (job.UploadMethod == Enums.UploadMethod.Cloud)
                return Accepted(job.ToResponse(BuildDownloadUrl));

            return Ok(job.ToResponse(BuildDownloadUrl));
        }
        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
        {
            logger.LogTrace(ex, "Starting job <{JobId}> failed.", jobId);
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Starting job <{JobId}> failed unexpectedly.", jobId);
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
        fileProvider.Initialize(jobId);

        if (!fileProvider.Exists(file))
        {
            logger.LogTrace("No file <{File}> found for job id <{JobId}>", HttpUtility.HtmlEncode(file), jobId);
            return Problem($"No file <{file}> found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
        }

        var stream = fileProvider.Open(file);
        var contentType = contentTypeProvider.GetContentTypeAsString(file);
        return File(stream, contentType, Path.GetFileName(file));
    }

    private Uri BuildDownloadUrl(Guid jobId, string fileName)
    {
        var url = Url.RouteUrl(nameof(Download), new { jobId, file = fileName }, Request.Scheme, Request.Host.Value)
            ?? throw new InvalidOperationException($"Could not generate download URL for job <{jobId}> file <{fileName}>.");
        return new Uri(url);
    }
}
