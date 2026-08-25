using Asp.Versioning;
using Geopilot.Api.Contracts;
using Geopilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Controller for file uploads.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("2.0")]
[AllowAnonymous]
public class UploadController : ControllerBase
{
    private readonly ILogger<UploadController> logger;
    private readonly IUploadOrchestrationService orchestrationService;
    private readonly UploadOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadController"/> class.
    /// </summary>
    public UploadController(ILogger<UploadController> logger, IOptions<UploadOptions> options, IUploadOrchestrationService orchestrationService)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.logger = logger;
        this.options = options.Value;
        this.orchestrationService = orchestrationService;
    }

    /// <summary>
    /// Returns the upload settings.
    /// </summary>
    /// <returns>Configuration settings for uploads.</returns>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "The upload settings.", typeof(UploadSettingsResponse), "application/json")]
    public IActionResult GetUploadSettings()
    {
        return Ok(new UploadSettingsResponse(
            options.MaxFileSizeMB,
            options.MaxFilesPerJob,
            options.MaxJobSizeMB));
    }

    /// <summary>
    /// Initiates a upload session by generating presigned URLs for the specified files.
    /// </summary>
    /// <param name="request">The upload request containing file metadata.</param>
    /// <returns>The upload response with presigned URLs and job information.</returns>
    [HttpPost]
    [EnableRateLimiting("uploadRateLimit")]
    [SwaggerResponse(StatusCodes.Status201Created, "The upload session was successfully created.", typeof(InitiateUploadResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The request is invalid.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Too many requests.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected error.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> InitiateUploadAsync([FromBody] InitiateUploadRequest request)
    {
        try
        {
            logger.LogInformation("Upload session initiated.");
            var response = await orchestrationService.InitiateUploadAsync(request);
            logger.LogInformation("Upload session created for upload <{UploadId}>.", response.UploadId);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            logger.LogTrace(ex, "Upload initiation failed.");
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Upload initiation failed unexpectedly.");
            return Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
