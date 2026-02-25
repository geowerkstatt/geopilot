using Api;
using Asp.Versioning;
using Geopilot.Api.Contracts;
using Geopilot.Api.Exceptions;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Swashbuckle.AspNetCore.Annotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Web;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Controller for file validation.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[AllowAnonymous]
public class ValidationController : ControllerBase
{
    private readonly ILogger<ValidationController> logger;
    private readonly IValidationService validationService;
    private readonly IFileProvider fileProvider;
    private readonly IContentTypeProvider contentTypeProvider;
    private readonly Context context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationController"/> class.
    /// </summary>
    public ValidationController(ILogger<ValidationController> logger, IValidationService validationService, IFileProvider fileProvider, IContentTypeProvider contentTypeProvider, Context context)
    {
        this.logger = logger;
        this.validationService = validationService;
        this.fileProvider = fileProvider;
        this.contentTypeProvider = contentTypeProvider;
        this.context = context;
    }

    /// <summary>
    /// Returns the validation settings.
    /// </summary>
    /// <returns>Configuration settings for validations.</returns>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "The specified settings for uploading files.", typeof(ValidationSettingsResponse), "application/json")]
    public async Task<IActionResult> GetValidationSettings()
    {
        return Ok(new ValidationSettingsResponse
        {
            AllowedFileExtensions = await validationService.GetSupportedFileExtensionsAsync(),
        });
    }

    /// <summary>
    /// Schedules a new job for the given <paramref name="file"/>.
    /// </summary>
    /// <param name="version">The API version.</param>
    /// <param name="file">The file to validate.</param>
    /// <remarks>
    /// ## Usage
    ///
    /// ### CURL
    ///
    /// ```bash
    /// curl -i -X POST -H "Content-Type: multipart/form-data" \
    ///   -F 'file=@example.xtf' https://example.com/api/v1/validation
    /// ```
    ///
    /// ### JavaScript
    ///
    /// ```javascript
    /// import { createReadStream } from 'fs';
    /// import FormData from 'form-data';
    /// import fetch from 'node-fetch';
    ///
    /// var form = new FormData();
    /// form.append('file', createReadStream('example.xtf'));
    /// const response = await fetch('https://example.com/api/v1/validation', {
    ///   method: 'POST',
    ///   body: form,
    /// });
    /// ```
    ///
    /// ### Python
    ///
    /// ```python
    /// import requests
    /// response = requests.post('https://example.com/api/v1/validation', files={'file':open('example.xtf')}).json()
    /// ```
    /// </remarks>
    /// <returns>Information for a newly created validation job.</returns>
    [HttpPost]
    [SwaggerResponse(StatusCodes.Status201Created, "The validation job was successfully created and is now scheduled for execution.", typeof(ValidationJobResponse), "application/json")]
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
        if (!await validationService.IsFileExtensionSupportedAsync(fileExtension))
        {
            logger.LogTrace("File extension <{FileExtension}> is not supported.", fileExtension);
            return Problem($"File extension <{fileExtension}> is not supported.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var validationJob = validationService.CreateJob();
            var fileHandle = validationService.CreateFileHandleForJob(validationJob.Id, file.FileName);

            using (fileHandle)
            {
                logger.LogInformation("Start uploading <{FormFile}> as <{File}>, file size: {FileSize}", file.FileName, fileHandle.FileName, file.Length);
                await file.CopyToAsync(fileHandle.Stream).ConfigureAwait(false);
                validationJob = validationService.AddFileToJob(validationJob.Id, file.FileName, fileHandle.FileName);
                logger.LogInformation("Successfully received file: {File}", fileHandle.FileName);
            }

            var location = new Uri(
            string.Format(CultureInfo.InvariantCulture, "/api/v{0}/validation/{1}", version.MajorVersion, validationJob.Id),
            UriKind.Relative);

            return CreatedAtAction(nameof(GetStatus), new { jobId = validationJob.Id }, validationJob.ToResponse());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "File upload failed.");
            return Problem("An unexpected error occured.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Starts the job with the specified <paramref name="jobId"/>. If a mandate is specified in the <paramref name="startJobRequest"/>, the job will be started with specified mandate.
    /// Otherwise, the job will be started without a mandate.
    /// </summary>
    /// <remarks>
    /// If a mandate id is provided, the user must be authenticated and authorized to use the specified mandate.
    /// Also, the mandate must support the file type the uploaded file of the specified job.
    /// </remarks>
    /// <param name="jobId">The id of the job that should be started.</param>
    /// <param name="startJobRequest"><see cref="StartJobRequest"/> containing all information to start the job.</param>
    /// <returns>The started validation job.</returns>
    [HttpPatch("{jobId}")]
    [SwaggerResponse(StatusCodes.Status200OK, "The validation job was successfully started.", typeof(ValidationJob), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request, or the mandate is not valid for the user/job.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The job with the specified jobId cannot be found.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected error while starting the job.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> StartJobAsync(Guid jobId, [FromBody] StartJobRequest startJobRequest)
    {
        ArgumentNullException.ThrowIfNull(startJobRequest);

        var job = validationService.GetJob(jobId);
        if (job == null)
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
            var validationJob = await validationService.StartJobAsync(jobId, startJobRequest.MandateId, user);
            logger.LogInformation("Job with id <{JobId}> is scheduled for execution.", validationJob.Id);
            return Ok(validationJob.ToResponse());
        }
        catch (CloudUploadPreflightException ex)
        {
            string detail;
            if (ex.FailureReason == PreflightFailureReason.ThreatDetected)
            {
                logger.LogError(ex, "Threat detected in upload for job <{JobId}>.", jobId);
                detail = "The upload could not be processed.";
            }
            else
            {
                logger.LogWarning(ex, "Preflight checks failed for job <{JobId}>.", jobId);
                detail = ex.Message;
            }

            return Problem(detail, statusCode: StatusCodes.Status400BadRequest);
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
    /// <param name="jobId" example="2e71ae96-e6ad-4b67-b817-f09412d09a2c">The job identifier.</param>
    /// <returns>The status information for the specified <paramref name="jobId"/>.</returns>
    [HttpGet("{jobId}")]
    [SwaggerResponse(StatusCodes.Status200OK, "The job with the specified jobId was found.", typeof(ValidationJobResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The job with the specified jobId cannot be found.", typeof(ProblemDetails), "application/json")]
    public IActionResult GetStatus(Guid jobId)
    {
        logger.LogTrace("Status for job <{JobId}> requested.", jobId);

        var job = validationService.GetJob(jobId);
        if (job == null)
        {
            logger.LogTrace("No job information available for job id <{JobId}>", jobId);
            return Problem($"No job information available for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(job.ToResponse());
    }

    /// <summary>
    /// Download the log file specified by the job id and file name.
    /// </summary>
    /// <param name="jobId" example="2e71ae96-e6ad-4b67-b817-f09412d09a2c">The job identifier.</param>
    /// <param name="file">The file name.</param>
    /// <returns>The <paramref name="file"/> for the specified <paramref name="jobId"/>.</returns>
    [HttpGet("{jobId}/files/{file}")]
    [SwaggerResponse(StatusCodes.Status200OK, "The specified log file was found.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The job or log file cannot be found.", typeof(ProblemDetails), "application/json")]
    public IActionResult Download(Guid jobId, string file)
    {
        logger.LogInformation("Download file <{File}> for job <{JobId}> requested.", HttpUtility.HtmlEncode(file), jobId);
        fileProvider.Initialize(jobId);

        var validationJob = validationService.GetJob(jobId);
        if (validationJob == null)
        {
            logger.LogTrace("No job information available for job id <{JobId}>", jobId);
            return Problem($"No job information available for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
        }

        if (!fileProvider.Exists(file))
        {
            logger.LogTrace("No log file <{File}> found for job id <{JobId}>", HttpUtility.HtmlEncode(file), jobId);
            return Problem($"No log file <{file}> found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
        }

        var logFile = fileProvider.Open(file);
        var contentType = contentTypeProvider.GetContentTypeAsString(file);
        var logFileName = Path.GetFileNameWithoutExtension(validationJob.OriginalFileName) + "_log" + Path.GetExtension(file);
        return File(logFile, contentType, logFileName);
    }
}
