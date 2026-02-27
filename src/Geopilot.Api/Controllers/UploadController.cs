using System.Threading.RateLimiting;
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
/// Controller for cloud file uploads.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("2.0")]
[AllowAnonymous]
public class UploadController : ControllerBase
{
    private readonly ILogger<UploadController> logger;
    private readonly ICloudOrchestrationService? orchestrationService;
    private readonly CloudStorageOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadController"/> class.
    /// </summary>
    public UploadController(ILogger<UploadController> logger, IOptions<CloudStorageOptions> options, ICloudOrchestrationService? orchestrationService = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.logger = logger;
        this.options = options.Value;
        this.orchestrationService = orchestrationService;
    }

    /// <summary>
    /// Returns the cloud upload settings.
    /// </summary>
    /// <returns>Configuration settings for cloud uploads.</returns>
    [HttpGet]
    [SwaggerResponse(StatusCodes.Status200OK, "The cloud upload settings.", typeof(UploadSettingsResponse), "application/json")]
    public IActionResult GetUploadSettings()
    {
        return Ok(new UploadSettingsResponse(
            options.Enabled,
            options.MaxFileSizeMB,
            options.MaxFilesPerJob,
            options.MaxJobSizeMB));
    }

    /// <summary>
    /// Initiates a cloud upload session by generating presigned URLs for the specified files.
    /// </summary>
    /// <param name="request">The upload request containing file metadata.</param>
    /// <returns>The upload response with presigned URLs and job information.</returns>
    [HttpPost]
    [EnableRateLimiting("uploadRateLimit")]
    [SwaggerResponse(StatusCodes.Status201Created, "The cloud upload session was successfully created.", typeof(CloudUploadResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The request is invalid.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status429TooManyRequests, "Too many requests.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected error.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> InitiateUploadAsync([FromBody] CloudUploadRequest request)
    {
        if (!options.Enabled || orchestrationService == null)
            return Problem("Cloud storage uploads are not enabled.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            logger.LogInformation("Cloud upload session initiated.");
            var response = await orchestrationService.InitiateUploadAsync(request);
            logger.LogInformation("Cloud upload session created for job <{JobId}>.", response.JobId);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            logger.LogTrace(ex, "Cloud upload initiation failed.");
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cloud upload initiation failed unexpectedly.");
            return Problem("An unexpected error occurred.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
