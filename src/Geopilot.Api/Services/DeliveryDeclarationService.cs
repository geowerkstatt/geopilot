using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;
using Microsoft.EntityFrameworkCore;

namespace Geopilot.Api.Services;

/// <inheritdoc cref="IDeliveryDeclarationService"/>
public class DeliveryDeclarationService : IDeliveryDeclarationService
{
    private readonly ILogger<DeliveryDeclarationService> logger;
    private readonly Context context;
    private readonly IProcessingService processingService;
    private readonly IMandateService mandateService;
    private readonly IAssetHandler assetHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryDeclarationService"/> class.
    /// </summary>
    public DeliveryDeclarationService(
        ILogger<DeliveryDeclarationService> logger,
        Context context,
        IProcessingService processingService,
        IMandateService mandateService,
        IAssetHandler assetHandler)
    {
        this.logger = logger;
        this.context = context;
        this.processingService = processingService;
        this.mandateService = mandateService;
        this.assetHandler = assetHandler;
    }

    /// <inheritdoc/>
    public async Task<DeliveryDeclarationResult> DeclareAsync(Guid jobId, DeliveryFields fields, int declaringUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var job = processingService.GetJob(jobId);
        if (job == null)
        {
            logger.LogTrace("No job information available for job with id <{JobId}>.", jobId);
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.JobNotFound, Message: $"No job information available for job with id <{jobId}>");
        }

        if (job.Pipeline is null || !job.Pipeline.State.IsDeliverable())
        {
            logger.LogTrace("Job with id <{JobId}> is not completed or delivery is not allowed.", jobId);
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.JobNotDeliverable, Message: $"Job with id <{jobId}> is not completed or delivery is not allowed.");
        }

        if (job.MandateId == null)
        {
            logger.LogTrace("Job with id <{JobId}> cannot be used to make a delivery.", jobId);
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.JobWithoutMandate, Message: $"Job with id <{jobId}> cannot be used to make a delivery.");
        }

        var user = await context.Users.SingleAsync(u => u.Id == declaringUserId, cancellationToken);

        // Do not reuse the mandate returned from GetMandateForUser, because it is not tracked and has no includes.
        var hasMandatePermission = await mandateService.GetMandateForUser(job.MandateId.Value, user) != null;
        var mandate = hasMandatePermission
            ? await context.Mandates.Include(m => m.Deliveries).FirstOrDefaultAsync(m => m.Id == job.MandateId, cancellationToken)
            : null;

        if (mandate is null || !mandate.AllowDelivery)
        {
            logger.LogTrace("Mandate with id <{MandateId}> not found.", job.MandateId);
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.MandateNotDeliverable, Message: $"Mandate with id <{job.MandateId}> not found.");
        }

        var precursorDelivery = mandate.Deliveries.SingleOrDefault(d => d.Id == fields.PrecursorDeliveryId);
        var fieldErrors = ValidateFields(mandate, fields, precursorDelivery);
        if (fieldErrors.Count > 0)
        {
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.FieldRulesViolated, FieldErrors: fieldErrors);
        }

        var delivery = new Delivery
        {
            JobId = jobId,
            Mandate = mandate,
            DeclaringUser = user,
            PrecursorDelivery = precursorDelivery,
            Partial = fields.PartialDelivery,
            Comment = fields.Comment?.Trim() ?? string.Empty,
            Assets = new List<Asset>(),
        };

        try
        {
            delivery.Assets.AddRange(await assetHandler.RecordJobAssetsAsync(jobId, cancellationToken));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while persisting assets for job with id <{JobId}>.", jobId);
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.AssetRecordingFailed, Message: $"Error while persisting assets for job with id <{jobId}>.");
        }

        if (delivery.Assets.Count == 0)
        {
            logger.LogWarning("No assets found for job with id <{JobId}>.", jobId);
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.NoAssets, Message: $"No assets found for job with id <{jobId}>.");
        }

        var entityEntry = context.Deliveries.Add(delivery);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Declaration for job with id <{JobId}> created.", jobId);
        return new DeliveryDeclarationResult(DeliveryDeclarationStatus.Created, entityEntry.Entity.Id);
    }

    /// <summary>
    /// Checks the delivery fields against the evaluation the mandate configures for each of them.
    /// </summary>
    /// <returns>The violated rules keyed by field name, empty when all fields are acceptable.</returns>
    private static Dictionary<string, string[]> ValidateFields(Mandate mandate, DeliveryFields fields, Delivery? precursorDelivery)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void AddError(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
            {
                messages = new List<string>();
                errors[field] = messages;
            }

            messages.Add(message);
        }

        if (mandate.EvaluatePrecursorDelivery == FieldEvaluationType.NotEvaluated && fields.PrecursorDeliveryId.HasValue)
        {
            AddError(nameof(fields.PrecursorDeliveryId), "Precursor delivery is not allowed for this mandate.");
        }
        else if (mandate.EvaluatePrecursorDelivery == FieldEvaluationType.Required && !fields.PrecursorDeliveryId.HasValue)
        {
            AddError(nameof(fields.PrecursorDeliveryId), "Precursor delivery is required for this mandate.");
        }

        if (fields.PrecursorDeliveryId.HasValue && precursorDelivery is null)
        {
            AddError(nameof(fields.PrecursorDeliveryId), "Precursor delivery not found.");
        }

        if (mandate.EvaluatePartial == FieldEvaluationType.NotEvaluated && fields.PartialDelivery.HasValue)
        {
            AddError(nameof(fields.PartialDelivery), "Partial delivery is not allowed for this mandate.");
        }
        else if (mandate.EvaluatePartial == FieldEvaluationType.Required && !fields.PartialDelivery.HasValue)
        {
            AddError(nameof(fields.PartialDelivery), "Partial delivery is required for this mandate.");
        }

        if (mandate.EvaluateComment == FieldEvaluationType.NotEvaluated && !string.IsNullOrWhiteSpace(fields.Comment))
        {
            AddError(nameof(fields.Comment), "Comment is not allowed for this mandate.");
        }
        else if (mandate.EvaluateComment == FieldEvaluationType.Required && string.IsNullOrWhiteSpace(fields.Comment))
        {
            AddError(nameof(fields.Comment), "Comment is required for this mandate.");
        }

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal);
    }
}
