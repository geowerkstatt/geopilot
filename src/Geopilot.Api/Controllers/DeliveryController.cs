using Geopilot.Api.Authorization;
using Geopilot.Api.Contracts;
using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Reactions;
using Geopilot.Api.Services;
using Geopilot.Api.StacServices;
using Geopilot.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq.Expressions;

namespace Geopilot.Api.Controllers;

/// <summary>
/// Controller for creating deliveries.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class DeliveryController : ControllerBase
{
    private readonly ILogger<DeliveryController> logger;
    private readonly Context context;
    private readonly IDeliveryCreationService deliveryCreationService;
    private readonly IMandateService mandateService;
    private readonly IAssetHandler assetHandler;
    private readonly IOptions<DeliveryOptions> deliveryOptions;
    private readonly IAuthorizationService authorizationService;
    private readonly ReactionStore reactionStore;
    private readonly IRecorder recorder;
    private readonly IPipelineFactory pipelineFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryController"/> class.
    /// </summary>
    public DeliveryController(ILogger<DeliveryController> logger, Context context, IDeliveryCreationService deliveryCreationService, IMandateService mandateService, IAssetHandler assetHandler, IOptions<DeliveryOptions> deliveryOptions, IAuthorizationService authorizationService, ReactionStore reactionStore, IRecorder recorder, IPipelineFactory pipelineFactory)
    {
        this.authorizationService = authorizationService;
        this.reactionStore = reactionStore;
        this.recorder = recorder;
        this.pipelineFactory = pipelineFactory;
        this.logger = logger;
        this.context = context;
        this.deliveryCreationService = deliveryCreationService;
        this.mandateService = mandateService;
        this.assetHandler = assetHandler;
        this.deliveryOptions = deliveryOptions;
    }

    /// <summary>
    /// Create a delivery from a successfully processed job whose pipeline allows delivery.
    /// </summary>
    /// <param name="request"><see cref="DeliveryRequest"/> containing all information for creating the delivery.</param>
    /// <returns>Created <see cref="Delivery"/>.</returns>
    [HttpPost]
    [Authorize(Policy = GeopilotPolicies.User)]
    [SwaggerResponse(StatusCodes.Status201Created, "The delivery was created successfully.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "The user is not authorized.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The validation job could not be found.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "The job was already delivered.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. Likely there was an error persisting the assets.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> Create(DeliveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation("Delivery for job with id <{JobId}> requested.", request.JobId);

        var user = await context.GetUserByPrincipalAsync(User);
        var fields = new DeliveryFields(request.PartialDelivery, request.PrecursorDeliveryId, request.Comment, request.Metadata);
        DeliveryCreationResult result;
        try
        {
            result = await deliveryCreationService.CreateAsync(request.JobId, fields, Supplier.ForUser(user.Id), HttpContext.RequestAborted);
        }
        catch (Exception)
        {
            await RecordDeliveryFailureAsync(request.JobId, DeliveryCreationResult.UnexpectedFailureDescription);
            throw;
        }

        if (result is not { Status: DeliveryCreationStatus.Created, DeliveryId: int deliveryId })
        {
            if (IsRefusalToRecord(result.Status))
                await RecordDeliveryFailureAsync(request.JobId, result.DescribeFailure());

            return MapCreationFailure(result);
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
    /// The refusals that can meet a run whose data allow a delivery its starter may make. The others say that the run
    /// does not allow one (or not yet), or that it was delivered already, which the protocol shows without a reason.
    /// </summary>
    private static bool IsRefusalToRecord(DeliveryCreationStatus status) => status
        is DeliveryCreationStatus.JobNotFound
        or DeliveryCreationStatus.MandateNotDeliverable
        or DeliveryCreationStatus.FieldRulesViolated
        or DeliveryCreationStatus.AssetRecordingFailed
        or DeliveryCreationStatus.NoAssets;

    /// <summary>
    /// Keeps in the execution protocol why the delivery was not created, as for a machine delivery, because otherwise
    /// only the browser learns of it. A request the browser aborted was not refused, so it leaves nothing.
    /// </summary>
    private async Task RecordDeliveryFailureAsync(Guid jobId, string reason)
    {
        if (HttpContext.RequestAborted.IsCancellationRequested)
            return;

        await recorder.RecordDeliveryFailedAsync(jobId, reason);
    }

    /// <summary>
    /// Maps a failed creation onto the response this endpoint has always returned for it.
    /// </summary>
    private IActionResult MapCreationFailure(DeliveryCreationResult result)
    {
        switch (result.Status)
        {
            case DeliveryCreationStatus.JobNotFound:
            case DeliveryCreationStatus.MandateNotDeliverable:
                return NotFound(result.Message);

            case DeliveryCreationStatus.JobNotDeliverable:
            case DeliveryCreationStatus.JobNotPermitted:
            case DeliveryCreationStatus.JobWithoutMandate:
                return BadRequest(result.Message);

            case DeliveryCreationStatus.AlreadyDelivered:
                return Conflict(result.Message);

            case DeliveryCreationStatus.FieldRulesViolated:
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

        var mandate = await mandateService.GetMandateForDeliveryAsync(mandateId, Supplier.ForUser(user.Id));
        if (mandate == null)
            return NotFound();

        var deliveries = context.Deliveries
            .AsNoTracking()
            .Where(d => d.Mandate != null && d.Mandate.Id == mandateId);

        var result = await deliveries.ToSummaries().ToListAsync();
        return Ok(result);
    }

    /// <summary>
    /// Lists the deliveries that match the <paramref name="query"/> with how far their reactions got, newest first,
    /// one page at a time.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "One page of deliveries, newest first.", typeof(DeliveryPageResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The period ends before it starts, or the page, a filter or the id prefix is out of bounds.", typeof(ValidationProblemDetails), "application/json")]
    public async Task<IActionResult> Get([FromQuery] DeliveryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.From > query.To)
            ModelState.AddModelError(nameof(query.To), "The period must not end before it starts.");
        if (!ModelState.IsValid)
            return ValidationProblem(statusCode: StatusCodes.Status400BadRequest, modelStateDictionary: ModelState);

        var user = await context.GetUserByPrincipalAsync(User);
        logger.LogInformation("User <{UserId}> accessed the list of deliveries", user.AuthIdentifier);

        var deliveries = FilterDeliveries(context.Deliveries.AsNoTracking(), context.DeliveryReactions, query);
        var totalCount = await deliveries.CountAsync(cancellationToken);
        var pageDeliveries = await deliveries
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.Id)
            .Skip(query.Page * query.PageSize)
            .Take(query.PageSize)
            .Select(d => new
            {
                d.Id,
                d.Date,
                Mandate = new MandateReference(d.Mandate.Id, d.Mandate.Name),
                DeliveredByName = d.DeliveredByUser != null ? d.DeliveredByUser.FullName : (d.DeliveredByClient != null ? d.DeliveredByClient.Name : string.Empty),
                d.DeliveredByClientId,
                Reactions = context.DeliveryReactions
                    .Where(r => r.DeliveryId == d.Id)
                    .Select(r => new { r.Kind, r.State })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return Ok(new DeliveryPageResponse(totalCount, pageDeliveries
            .Select(d => new DeliveryResponse(
                d.Id,
                d.Date,
                d.Mandate,
                d.DeliveredByName,
                d.DeliveredByClientId,
                d.Reactions.Where(r => r.Kind == ReactionKind.Transfer).Select(r => r.State).SummedUp(),
                d.Reactions.Where(r => r.Kind == ReactionKind.Notification).Select(r => r.State).SummedUp()))
            .ToList()));
    }

    private static IQueryable<Delivery> FilterDeliveries(IQueryable<Delivery> deliveries, IQueryable<DeliveryReaction> reactions, DeliveryQuery query)
    {
        var mandateIds = query.MandateIds;
        var userIds = query.UserIds;
        var machineClientIds = query.MachineClientIds;
        var failedReactionKinds = query.FailedReactionKinds;

        if (query.From?.AsUtc() is { } from)
            deliveries = deliveries.Where(d => d.Date >= from);
        if (query.To?.AsUtc() is { } to)
            deliveries = deliveries.Where(d => d.Date < to);
        if (mandateIds.Count > 0)
            deliveries = deliveries.Where(d => mandateIds.Contains(d.Mandate.Id));
        if (userIds.Count > 0 || machineClientIds.Count > 0)
        {
            deliveries = deliveries.Where(d =>
                (d.DeliveredByUserId != null && userIds.Contains(d.DeliveredByUserId.Value))
                || (d.DeliveredByClientId != null && machineClientIds.Contains(d.DeliveredByClientId.Value)));
        }

        // Error comes first when the column sums the reactions of a kind up, so a delivery matches exactly when the
        // column of a chosen kind shows Error.
        if (failedReactionKinds.Count > 0)
        {
            var failed = reactions
                .Where(r => r.State == ReactionState.Error && failedReactionKinds.Contains(r.Kind))
                .Select(r => r.DeliveryId);
            deliveries = deliveries.Where(d => failed.Contains(d.Id));
        }

        // The id as text, by concatenation, since ToString with a culture does not translate to SQL. The prefix holds
        // digits only, so it holds no wildcard either.
        if (query.IdPrefix is { } idPrefix)
            deliveries = deliveries.Where(d => EF.Functions.Like(string.Empty + d.Id, $"{idPrefix}%"));

        return deliveries;
    }

    /// <summary>
    /// Gets a delivery with what was delivered and how far its reactions got.
    /// </summary>
    /// <param name="deliveryId">The delivery to get.</param>
    /// <returns>The <see cref="DeliveryDetailResponse"/>.</returns>
    [HttpGet("{deliveryId}")]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status200OK, "The delivery.", typeof(DeliveryDetailResponse), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The delivery could not be found.")]
    public async Task<IActionResult> GetById([FromRoute] int deliveryId)
    {
        var user = await context.GetUserByPrincipalAsync(User);
        logger.LogInformation("User <{UserId}> accessed delivery <{DeliveryId}>", user.AuthIdentifier, deliveryId);

        var delivery = await context.Deliveries
            .AsNoTracking()
            .Where(d => d.Id == deliveryId)
            .Select(d => new
            {
                d.Id,
                d.Date,
                Mandate = new MandateReference(d.Mandate.Id, d.Mandate.Name),
                DeliveredByName = d.DeliveredByUser != null ? d.DeliveredByUser.FullName : (d.DeliveredByClient != null ? d.DeliveredByClient.Name : string.Empty),
                d.DeliveredByUserId,
                d.DeliveredByClientId,
                PrecursorDelivery = d.PrecursorDelivery != null ? new DeliverySummary(d.PrecursorDelivery.Id, d.PrecursorDelivery.Date) : null,
                d.Partial,
                d.JobId,
                Files = d.Assets.OrderBy(a => a.Id).Select(a => new AssetReference(a.Id, a.OriginalFilename)).ToList(),
                d.Comment,
                d.Metadata,
            })
            .SingleOrDefaultAsync();
        if (delivery is null)
            return NotFound($"No delivery with id <{deliveryId}> found.");

        var reactions = await context.DeliveryReactions
            .AsNoTracking()
            .Where(r => r.DeliveryId == deliveryId)
            .OrderBy(r => r.Id)
            .Select(r => new DeliveryReactionResponse(
                r.ReactionId,
                null,
                r.Kind,
                r.State,
                r.Attempts,
                r.State == ReactionState.Pending ? r.NextAttemptAt : (DateTime?)null,
                r.LastAttemptAt,
                r.FinishedAt,
                r.Message))
            .ToListAsync();

        // The STAC catalog shows a delivery once its uploader can no longer delete it and all its transfers are done.
        var forwarded = reactions.Where(r => r.Kind == ReactionKind.Transfer).Select(r => r.State).SummedUp() is null or ReactionState.Done;
        var inStac = forwarded && deliveryOptions.Value.GetDeleteWindowEnd(delivery.Date) <= DateTime.UtcNow;
        var stacItem = inStac ? new StacItemReference(StacConverter.CollectionId(delivery.Mandate.Id), StacConverter.ItemId(delivery.Id)) : null;

        var displayNames = pipelineFactory.ReactionDisplayNames();

        return Ok(new DeliveryDetailResponse(
            delivery.Id,
            delivery.Date,
            delivery.Mandate,
            delivery.DeliveredByName,
            delivery.DeliveredByUserId,
            delivery.DeliveredByClientId,
            delivery.PrecursorDelivery,
            delivery.Partial,
            delivery.JobId,
            delivery.Files,
            delivery.Comment,
            delivery.Metadata,
            stacItem,
            reactions.Select(r => r with { DisplayName = displayNames.GetValueOrDefault(r.ReactionId) }).ToList()));
    }

    /// <summary>
    /// Resets a reaction of a delivery that gave up: it gets all its attempts again and is due at once.
    /// </summary>
    /// <param name="deliveryId">The delivery of the reaction.</param>
    /// <param name="reactionId">The reaction to reset.</param>
    [HttpPost("{deliveryId}/reactions/{reactionId}/reset")]
    [Authorize(Policy = GeopilotPolicies.Admin)]
    [SwaggerResponse(StatusCodes.Status204NoContent, "The reaction is due again.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The delivery or its reaction could not be found.")]
    [SwaggerResponse(StatusCodes.Status409Conflict, "The reaction has not failed.")]
    public async Task<IActionResult> ResetReaction([FromRoute] int deliveryId, [FromRoute] string reactionId)
    {
        var user = await context.GetUserByPrincipalAsync(User);
        if (!await context.Deliveries.AnyAsync(d => d.Id == deliveryId)
            || !await context.DeliveryReactions.AnyAsync(r => r.DeliveryId == deliveryId && r.ReactionId == reactionId))
            return NotFound($"Delivery <{deliveryId}> or its reaction could not be found.");

        // Not cancelled with the request, so a reset that happens also reaches the protocol.
        var reset = await reactionStore.ResetFailedAsync(context, deliveryId, reactionId, CancellationToken.None);
        if (reset is null)
            return Conflict($"The reaction of delivery <{deliveryId}> has not failed.");

        await recorder.RecordReactionsResetAsync(deliveryId, [reset], user.Id);
        logger.LogInformation("User <{UserId}> reset the failed reaction {ReactionId} of delivery <{DeliveryId}>.", user.AuthIdentifier, reset.ReactionId, deliveryId);
        return NoContent();
    }

    /// <summary>
    /// Gets the deliveries the user delivered, and those of the machine clients the user is responsible for.
    /// </summary>
    /// <returns>A list of <see cref="UploadedDelivery"/>.</returns>
    [HttpGet("uploads")]
    [Authorize(Policy = GeopilotPolicies.User)]
    [SwaggerResponse(StatusCodes.Status200OK, "A list of deliveries uploaded by the user or by the machine clients the user is responsible for.", typeof(List<UploadedDelivery>), "application/json")]
    public async Task<IActionResult> GetUploads()
    {
        var user = await context.GetUserByPrincipalAsync(User);

        logger.LogInformation(
            "User <{UserId}> accessed list of their uploaded deliveries",
            user.AuthIdentifier);

        // Whether a delivery may still be deleted depends on the configuration and the time, which SQL cannot evaluate,
        // and on whether one of its reactions has begun, which it can.
        var started = context.DeliveryReactions.DeliveryIdsWithStartedReactions();
        var deliveries = await context.Deliveries
            .AsNoTracking()
            .Where(DeliveredByOrForUser(user.Id))
            .OrderBy(d => d.Id)
            .Select(d => new
            {
                d.Id,
                d.Date,
                d.Comment,
                Mandate = new MandateReference(d.Mandate.Id, d.Mandate.Name),
                DeliveredByName = d.DeliveredByUser != null ? d.DeliveredByUser.FullName : (d.DeliveredByClient != null ? d.DeliveredByClient.Name : string.Empty),
                Started = started.Contains(d.Id),
            })
            .ToListAsync();

        return Ok(deliveries
            .Select(d => new UploadedDelivery(d.Id, d.Date, d.Mandate, d.DeliveredByName, d.Comment, !d.Started && IsDeleteAllowedForUploader(d.Date)))
            .ToList());
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
                deliveries = deliveries.Where(DeliveredByOrForUser(user.Id));
            }

            var delivery = await deliveries.SingleOrDefaultAsync(d => d.Id == deliveryId);
            if (delivery == default)
            {
                logger.LogTrace("No delivery with id <{DeliveryId}> found.", deliveryId);
                return NotFound($"No delivery with id <{deliveryId}> found.");
            }

            // The window decides on its own only while the configuration stays as it was at creation. A reaction
            // that has begun means the delivery left already, after a longer window or a later enabled deletion.
            // shortcut: checked before the deletion rather than with it, so a reaction claimed in between still runs once;
            // its attempt then fails on the deleted files or its report is refused.
            if (!user.IsAdmin && (!IsDeleteAllowedForUploader(delivery.Date)
                || await context.DeliveryReactions.DeliveryIdsWithStartedReactions().AnyAsync(id => id == delivery.Id)))
            {
                logger.LogTrace("Delete of delivery with id <{DeliveryId}> not allowed for uploader <{UserId}>.", deliveryId, user.AuthIdentifier);
                return Problem($"Deleting delivery with id <{deliveryId}> is not allowed.", statusCode: StatusCodes.Status403Forbidden);
            }

            delivery.Deleted = true;
            delivery.Assets.ForEach(a => a.Deleted = true);
            await context.SaveChangesAsync();

            await EndReactionsAsync(delivery.Id, user.Id);
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
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "A file has been downloaded.", typeof(File), "application/json")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "The server cannot process the request due to invalid or malformed request.", typeof(ValidationProblemDetails), "application/json")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The asset could not be found.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "The server encountered an unexpected condition that prevented it from fulfilling the request. Likely the file could not be read.", typeof(ProblemDetails), "application/json")]
    public async Task<IActionResult> DownloadAsync([FromRoute] int assetId)
    {
        logger.LogInformation("Downloading asset with id <{AssetId}> started.", assetId);
        try
        {
            var asset = context.Assets
                .Include(a => a.Delivery)
                .ThenInclude(d => d!.Mandate)
                .SingleOrDefault(a => a.Id == assetId && !a.Deleted);

            // A caller who may not download the asset learns as much as for an id that does not exist.
            if (asset == default || !await MayDownloadAsync(asset))
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

    // STAC links its assets here, so a file can be downloaded exactly when the catalog shows its delivery.
    private async Task<bool> MayDownloadAsync(Asset asset)
    {
        var isAdmin = (await authorizationService.AuthorizeAsync(User, GeopilotPolicies.Admin)).Succeeded;
        var deliveryId = asset.Delivery?.Id;
        return await context.DeliveriesInStac(isAdmin, deliveryOptions.Value).AnyAsync(d => d.Id == deliveryId);
    }

    /// <summary>
    /// Ends the open reactions of a deleted delivery and records in the protocol how far each had got. The delivery is
    /// deleted already and the reactions of a deleted delivery never run again, so a failure here only warns.
    /// </summary>
    private async Task EndReactionsAsync(int deliveryId, int userId)
    {
        try
        {
            // shortcut: does not wait for a running attempt. Its report is refused, and what its channel has written
            // already stays at the target (ADR 0015, Nachtrag: Löschen während einer laufenden Reaktion).
            var reactions = await reactionStore.MarkDeletedAsync(context, deliveryId, CancellationToken.None);
            await recorder.RecordDeliveryDeletedAsync(deliveryId, reactions, userId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ending the reactions of the deleted delivery <{DeliveryId}> failed.", deliveryId);
        }
    }

    /// <summary>
    /// The deliveries a user sees and deletes as their own: those the user delivered, and those of the machine clients
    /// the user is responsible for.
    /// </summary>
    private static Expression<Func<Delivery, bool>> DeliveredByOrForUser(int userId)
        => d => d.DeliveredByUserId == userId || (d.DeliveredByClient != null && d.DeliveredByClient.ResponsibleUsers.Any(u => u.Id == userId));

    internal bool IsDeleteAllowedForUploader(DateTime deliveredAt, DateTime? utcNow = null)
    {
        var options = deliveryOptions.Value;
        if (!options.UploaderDeleteEnabled)
        {
            return false;
        }

        var windowEnd = options.GetDeleteWindowEnd(deliveredAt);
        return windowEnd is null || (utcNow ?? DateTime.UtcNow) < windowEnd;
    }
}
