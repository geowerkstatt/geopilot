using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Controller for declaring deliveries.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class DeliveryController : ControllerBase
{
    private readonly ILogger<DeliveryController> logger;
    private readonly Context context;
    private readonly IDeliveryDeclarationService deliveryDeclarationService;
    private readonly IMandateService mandateService;
    private readonly IAssetHandler assetHandler;
    private readonly IOptions<DeliveryOptions> deliveryOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryController"/> class.
    /// </summary>
    public DeliveryController(ILogger<DeliveryController> logger, Context context, IDeliveryDeclarationService deliveryDeclarationService, IMandateService mandateService, IAssetHandler assetHandler, IOptions<DeliveryOptions> deliveryOptions)
    {
        this.logger = logger;
        this.context = context;
        this.deliveryDeclarationService = deliveryDeclarationService;
        this.mandateService = mandateService;
        this.assetHandler = assetHandler;
        this.deliveryOptions = deliveryOptions;
    }

    /// <summary>
    /// Create a delivery from a successfully processed job whose pipeline allows delivery.
    /// </summary>
    /// <param name="declaration"><see cref="DeliveryRequest"/> containing all information for the declaration process.</param>
    /// <returns>Created <see cref="Delivery"/>.</returns>
    [HttpPost]
    [Authorize(Policy = GeopilotPolicies.User)]
    [SwaggerResponse(StatusCodes.Status201Created, "The delivery was created successfully.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The user is not authorized.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The validation job could not be found.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "The job was already delivered.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. Likely there was an error persisting the assets.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> Create(DeliveryRequest declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        logger.LogInformation("Declaration for job with id <{JobId}> requested.", declaration.JobId);

        var user = await context.GetUserByPrincipalAsync(User);
        var fields = new DeliveryFields(declaration.PartialDelivery, declaration.PrecursorDeliveryId, declaration.Comment);
        var result = await deliveryDeclarationService.DeclareAsync(declaration.JobId, fields, user.Id, HttpContext.RequestAborted);

        if (result is not { Status: DeliveryDeclarationStatus.Created, DeliveryId: int deliveryId })
        {
            return MapDeclarationFailure(result);
        }

        var resultDelivery = context.Deliveries
            .AsNoTracking()
            .Single(d => d.Id == deliveryId);

        var location = new Uri(
            string.Format(CultureInfo.InvariantCulture, "/api/v1/delivery/{0}", deliveryId),
            UriKind.Relative);

        return Created(location, resultDelivery);
    }

    /// <summary>
    /// Maps a failed declaration onto the response this endpoint has always returned for it.
    /// </summary>
    private IActionResult MapDeclarationFailure(DeliveryDeclarationResult result)
    {
        switch (result.Status)
        {
            case DeliveryDeclarationStatus.JobNotFound:
            case DeliveryDeclarationStatus.MandateNotDeliverable:
                return NotFound(result.Message);

            case DeliveryDeclarationStatus.JobNotDeliverable:
            case DeliveryDeclarationStatus.JobWithoutMandate:
                return BadRequest(result.Message);

            case DeliveryDeclarationStatus.AlreadyDeclared:
                return Conflict(result.Message);

            case DeliveryDeclarationStatus.FieldRulesViolated:
                foreach (var (field, messages) in result.FieldErrors ?? ReadOnlyDictionary<string, string[]>.Empty)
                {
                    foreach (var message in messages)
                    {
                        ModelState.AddModelError(field, message);
                    }
                }

                return ValidationProblem(ModelState);

            default:
                return Problem(result.Message);
        }
    }

    /// <summary>
    /// Gets a filtered list of deliveries accessible for the user.
    /// </summary>
    /// <param name="mandateId">Filter deliveries for given mandate.</param>
    /// <returns>A list of <see cref="DeliverySummary"/>.</returns>
    [HttpGet("summary")]
    [Authorize(Policy = GeopilotPolicies.User)]
    [SwaggerResponse(StatusCodes.Status200OK, "A list matching filter criteria.", typeof(List<DeliverySummary>), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The request is missing a mandateId.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Failed to find mandate.")]
    public async Task<IActionResult> GetSummary([FromQuery] int mandateId)
    {
        if (mandateId == default)
        {
            return BadRequest("Mandate id is required.");
        }

        var user = await context.GetUserByPrincipalAsync(User);

        logger.LogInformation("User <{UserId}> accessed list of deliveries filtered by mandateId <{MandateId}>", user.AuthIdentifier, mandateId);

        var mandate = await mandateService.GetMandateForUser(mandateId, user);
        if (mandate == null)
            return NotFound();

        var deliveries = context.Deliveries
            .AsNoTracking()
            .Where(d => d.Mandate != null && d.Mandate.Id == mandateId);

        var result = await deliveries.ToSummaries().ToListAsync();
        return Ok(result);
    }

    /// <summary>
    /// Gets a list of all deliveries.
    /// </summary>
    /// <returns>A list of all <see cref="Delivery"/>.</returns>
    [HttpGet]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "A list of all deliveries.", typeof(List<Delivery>), "application/json")]
    public async Task<IActionResult> Get()
    {
        var user = await context.GetUserByPrincipalAsync(User);
        logger.LogInformation("User <{UserId}> accessed the list of all deliveries", user.AuthIdentifier);

        var result = context.DeliveriesWithIncludes
            .AsNoTracking();

        return Ok(await result.ToListAsync());
    }

    /// <summary>
    /// Gets a filtered list of deliveries uploaded by the user.
    /// </summary>
    /// <returns>A list of <see cref="Delivery"/>.</returns>
    [HttpGet("uploads")]
    [Authorize(Policy = GeopilotPolicies.User)]
    [SwaggerResponse(StatusCodes.Status200OK, "A list of deliveries uploaded by the user.", typeof(List<Delivery>), "application/json")]
    public async Task<IActionResult> GetUploads()
    {
        var user = await context.GetUserByPrincipalAsync(User);

        logger.LogInformation(
            "User <{UserId}> accessed list of their uploaded deliveries",
            user.AuthIdentifier);

        var result = await context.DeliveriesWithIncludes
            .AsNoTracking()
            .Where(d => d.DeclaringUser.Id == user.Id)
            .ToListAsync();

        foreach (var delivery in result)
        {
            delivery.CanDelete = IsDeleteAllowedForUploader(delivery);
        }

        return Ok(result);
    }

    /// <summary>
    /// Performs a soft delete in the database and deletes the files from the storage.
    /// </summary>
    /// <returns>An updated list of <see cref="Delivery"/>.</returns>
    [HttpDelete("{deliveryId}")]
    [Authorize(Policy = GeopilotPolicies.User)]
    [SwaggerResponse(StatusCodes.Status200OK, "The delivery was successfully deleted.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "The user is not allowed to delete the delivery. This can mean delete is disabled for uploaders or the time to delete has expired.", typeof(ProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The delivery could not be found.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. Likely there was an error deleting the assets.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> Delete([FromRoute] int deliveryId)
    {
        logger.LogInformation("Deleting of delivery with id <{DeliveryId}> started.", deliveryId);
        try
        {
            IQueryable<Delivery> deliveries = context.Deliveries.Include(d => d.Assets);

            var user = await context.GetUserByPrincipalAsync(User);
            if (!user.IsAdmin)
            {
                deliveries = deliveries.Where(d => d.DeclaringUser.Id == user.Id);
            }

            var delivery = await deliveries.SingleOrDefaultAsync(d => d.Id == deliveryId);
            if (delivery == default)
            {
                logger.LogTrace("No delivery with id <{DeliveryId}> found.", deliveryId);
                return NotFound($"No delivery with id <{deliveryId}> found.");
            }

            if (!user.IsAdmin && !IsDeleteAllowedForUploader(delivery))
            {
                logger.LogTrace("Delete of delivery with id <{DeliveryId}> not allowed for uploader <{UserId}>.", deliveryId, user.AuthIdentifier);
                return Problem($"Deleting delivery with id <{deliveryId}> is not allowed.", statusCode: StatusCodes.Status403Forbidden);
            }

            delivery.Deleted = true;
            delivery.Assets.ForEach(a => a.Deleted = true);
            await context.SaveChangesAsync();

            assetHandler.DeleteJobAssets(delivery.JobId);

            logger.LogInformation("Deleting of delivery with id <{DeliveryId}> successful.", deliveryId);
            return Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while deleting delivery with id <{DeliveryId}>.", deliveryId);
            return Problem($"Error while deleting delivery with id <{deliveryId}>.");
        }
    }

    /// <summary>
    /// Downloads an asset from the persistent storage.
    /// </summary>
    /// <returns>The asset file.</returns>
    [HttpGet("assets/{assetId}")]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "A file has been downloaded.", typeof(File), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The asset could not be found.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. Likely the file could not be read.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> DownloadAsync([FromRoute] int assetId)
    {
        logger.LogInformation("Downloading asset with id <{AssetId}> started.", assetId);
        try
        {
            var asset = context.Assets.Include(a => a.Delivery).SingleOrDefault(a => a.Id == assetId && !a.Deleted);
            if (asset == default)
            {
                logger.LogTrace("No asset with id <{AssetId}> found.", assetId);
                return NotFound($"No asset with id <{assetId}> found.");
            }

            if (asset.Delivery == null)
            {
                throw new InvalidOperationException($"Asset with id {assetId} has no delivery.");
            }

            var (content, contentType) = await assetHandler.DownloadAssetAsync(asset.Delivery.JobId, asset.SanitizedFilename);
            return File(content, contentType, asset.OriginalFilename);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while accessing delivery asset with id <{AssetId}>.", assetId);
            return Problem($"Error while accessing delivery asset with id <{assetId}>.");
        }
    }

    internal bool IsDeleteAllowedForUploader(Delivery delivery, DateTime? utcNow = null)
    {
        var options = deliveryOptions.Value;
        if (!options.UploaderDeleteEnabled)
        {
            return false;
        }

        utcNow ??= DateTime.UtcNow;

        var deliveryDateUtc = delivery.Date.Kind switch
        {
            DateTimeKind.Utc => delivery.Date,
            DateTimeKind.Local => delivery.Date.ToUniversalTime(),
            _ => DateTime.SpecifyKind(delivery.Date, DateTimeKind.Utc),
        };

        if (options.DeleteDuration is TimeSpan deleteDuration && deleteDuration <= utcNow - deliveryDateUtc)
        {
            return false;
        }

        if (options.DeleteRestrictIntervalExpression?.GetNextOccurrence(deliveryDateUtc) is DateTime nextOccurrence && nextOccurrence <= utcNow)
        {
            return false;
        }

        return true;
    }
}
