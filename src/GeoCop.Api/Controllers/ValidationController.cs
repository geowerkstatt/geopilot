using Asp.Versioning;
using GeoCop.Api.Contracts;
using GeoCop.Api.FileAccess;
using GeoCop.Api.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Swashbuckle.AspNetCore.Annotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace GeoCop.Api.Controllers;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationController"/> class.
    /// </summary>
    public ValidationController(ILogger<ValidationController> logger, IValidationService validationService, IFileProvider fileProvider, IContentTypeProvider contentTypeProvider)
    {
        this.logger = logger;
        this.validationService = validationService;
        this.fileProvider = fileProvider;
        this.contentTypeProvider = contentTypeProvider;
    }

    /// <summary>
    /// Returns the validation settings.
    /// </summary>
    /// <returns>Configuration settings for validations.</returns>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "The specified settings for uploading files.", typeof(ValidationSettingsResponse), new[] { "application/json" })]
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
    [SwaggerResponse(StatusCodes.Status201Created, "The validation job was successfully created and is now scheduled for execution.", typeof(ValidationJobStatus), new[] { "application/json" })]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ProblemDetails), new[] { "application/json" })]
    [SwaggerResponse(StatusCodes.Status413PayloadTooLarge, "The file is too large. Max allowed request body size is 200 MB.")]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1629:DocumentationTextMustEndWithAPeriod", Justification = "Not applicable for code examples.")]
    public async Task<IActionResult> UploadAsync(ApiVersion version, IFormFile file)
    {
        if (file == null) return Problem($"Form data <{nameof(file)}> cannot be empty.", statusCode: StatusCodes.Status400BadRequest);

        var fileExtension = Path.GetExtension(file.FileName);
        if (!await validationService.IsFileExtensionSupportedAsync(fileExtension))
        {
            logger.LogTrace("File extension <{FileExtension}> is not supported.", fileExtension);
            return Problem($"File extension <{fileExtension}> is not supported.", statusCode: StatusCodes.Status400BadRequest);
        }

        var (validationJob, fileHandle) = validationService.CreateValidationJob(file.FileName);
        using (fileHandle)
        {
            logger.LogInformation("Start uploading <{FormFile}> as <{File}>, file size: {FileSize}", file.FileName, fileHandle.FileName, file.Length);

            await file.CopyToAsync(fileHandle.Stream).ConfigureAwait(false);
            logger.LogInformation("Successfully received file: {File}", fileHandle.FileName);
        }

        var status = await validationService.StartValidationJobAsync(validationJob);
        logger.LogInformation("Job with id <{JobId}> is scheduled for execution.", validationJob.Id);

        var location = new Uri(
            string.Format(CultureInfo.InvariantCulture, "/api/v{0}/validation/{1}", version.MajorVersion, validationJob.Id),
            UriKind.Relative);

        return Created(location, status);
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

    /// <summary>
    /// Download the log file specified by the job id and file name.
    /// </summary>
    /// <param name="jobId" example="2e71ae96-e6ad-4b67-b817-f09412d09a2c">The job identifier.</param>
    /// <param name="file">The file name.</param>
    /// <returns>The <paramref name="file"/> for the specified <paramref name="jobId"/>.</returns>
    [HttpGet("{jobId}/files/{file}")]
    [SwaggerResponse(StatusCodes.Status200OK, "The specified log file was found.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), new[] { "application/json" })]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The job or log file cannot be found.", typeof(ProblemDetails), new[] { "application/json" })]
    public IActionResult Download(Guid jobId, string file)
    {
        logger.LogTrace("Download file <{File}> for job <{JobId}> requested.", file, jobId);

        fileProvider.Initialize(jobId);

        var validationJob = validationService.GetJob(jobId);
        if (validationJob == null)
        {
            logger.LogTrace("No job information available for job id <{JobId}>", jobId);
            return Problem($"No job information available for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
        }

        if (!fileProvider.Exists(file))
        {
            logger.LogTrace("No log file <{File}> found for job id <{JobId}>", file, jobId);
            return Problem($"No log file <{file}> found for job id <{jobId}>", statusCode: StatusCodes.Status404NotFound);
        }

        var logFile = fileProvider.Open(file);
        var contentType = contentTypeProvider.GetContentTypeAsString(file);
        var logFileName = Path.GetFileNameWithoutExtension(validationJob.OriginalFileName) + "_log" + Path.GetExtension(file);
        return File(logFile, contentType, logFileName);
    }
}
