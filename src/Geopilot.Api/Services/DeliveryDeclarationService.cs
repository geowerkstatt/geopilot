using Geopilot.Api.FileAccess;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Api.Reactions;
using Geopilot.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Text.Json;

namespace Geopilot.Api.Services;

/// <inheritdoc cref="IDeliveryDeclarationService"/>
public class DeliveryDeclarationService : IDeliveryDeclarationService
{
    private readonly ILogger<DeliveryDeclarationService> logger;
    private readonly Context context;
    private readonly IProcessingService processingService;
    private readonly IMandateService mandateService;
    private readonly IAssetHandler assetHandler;
    private readonly IPipelineFactory pipelineFactory;
    private readonly IOptions<DeliveryOptions> deliveryOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeliveryDeclarationService"/> class.
    /// </summary>
    public DeliveryDeclarationService(
        ILogger<DeliveryDeclarationService> logger,
        Context context,
        IProcessingService processingService,
        IMandateService mandateService,
        IAssetHandler assetHandler,
        IPipelineFactory pipelineFactory,
        IOptions<DeliveryOptions> deliveryOptions)
    {
        this.logger = logger;
        this.context = context;
        this.processingService = processingService;
        this.mandateService = mandateService;
        this.assetHandler = assetHandler;
        this.pipelineFactory = pipelineFactory;
        this.deliveryOptions = deliveryOptions;
    }

    /// <inheritdoc/>
    public async Task<DeliveryDeclarationResult> DeclareAsync(Guid jobId, DeliveryFields fields, Declarer declarer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(declarer);

        var job = processingService.GetJob(jobId);
        if (job == null)
        {
            logger.LogTrace("No job information available for job with id <{JobId}>.", jobId);
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.JobNotFound, Message: $"No job information available for job with id <{jobId}>");
        }

        if (!job.State.IsDeliverable())
        {
            logger.LogTrace("Job with id <{JobId}> is not completed or delivery is not allowed.", jobId);
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.JobNotDeliverable, Message: $"Job with id <{jobId}> is not completed or delivery is not allowed.");
        }

        if (job.MandateId == null)
        {
            logger.LogTrace("Job with id <{JobId}> cannot be used to make a delivery.", jobId);
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.JobWithoutMandate, Message: $"Job with id <{jobId}> cannot be used to make a delivery.");
        }

        // A job yields at most one delivery. Without this guard a client that lets geopilot declare the delivery
        // and then declares it again through the API would deliver the same job twice, and it also makes a repeated
        // declaration from the background safe.
        var existingDeliveryId = await FindDeliveryOfJobAsync(jobId, cancellationToken);
        if (existingDeliveryId is not null)
        {
            // The existing delivery comes back with the refusal, so a caller that ends up here can point at the
            // delivery it already has instead of treating the job as undelivered.
            logger.LogInformation("Job with id <{JobId}> was already delivered.", jobId);
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.AlreadyDeclared, existingDeliveryId, $"Job with id <{jobId}> was already delivered.");
        }

        // Loaded here rather than passed in: the declaration also runs outside a request, where an entity from
        // the request would belong to a context that no longer exists. Exactly one of the two is set.
        var user = declarer.UserId is int userId ? await context.Users.SingleAsync(u => u.Id == userId, cancellationToken) : null;
        var client = declarer.MachineClientId is int clientId ? await context.MachineClients.SingleAsync(c => c.Id == clientId, cancellationToken) : null;

        // Do not reuse the mandate returned from GetMandateForDeliveryAsync, because it is not tracked and has no includes.
        var hasMandatePermission = await mandateService.GetMandateForDeliveryAsync(job.MandateId.Value, declarer) != null;
        var mandate = hasMandatePermission
            ? await context.Mandates.Include(m => m.Deliveries).FirstOrDefaultAsync(m => m.Id == job.MandateId, cancellationToken)
            : null;

        if (mandate is null || !mandate.AllowDelivery)
        {
            logger.LogTrace("Mandate with id <{MandateId}> not found.", job.MandateId);
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.MandateNotDeliverable, Message: $"Mandate with id <{job.MandateId}> not found.");
        }

        var precursorDelivery = mandate.Deliveries.SingleOrDefault(d => d.Id == fields.PrecursorDeliveryId);
        var fieldErrors = DeliveryFieldValidator.Validate(mandate, fields, precursorDelivery);
        if (fieldErrors.Count > 0)
        {
            return new DeliveryDeclarationResult(DeliveryDeclarationStatus.FieldRulesViolated, FieldErrors: fieldErrors);
        }

        var delivery = new Delivery
        {
            JobId = jobId,
            Mandate = mandate,
            DeclaringUser = user,
            DeclaringClient = client,
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

        var transfers = CreateTransfers(delivery, mandate);
        var entityEntry = context.Deliveries.Add(delivery);
        context.DeliveryReactions.AddRange(transfers);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (IsJobConflict(e))
        {
            // Another caller declared the same job between the check above and this write. The unique index is
            // what actually keeps a job to one delivery; the check only spares the common case a failed insert.
            foreach (var transfer in transfers)
            {
                context.Entry(transfer).State = EntityState.Detached;
            }

            context.Entry(delivery).State = EntityState.Detached;
            logger.LogInformation("Job with id <{JobId}> was delivered by another caller while this delivery was being written.", jobId);
            return new DeliveryDeclarationResult(
                DeliveryDeclarationStatus.AlreadyDeclared,
                await FindDeliveryOfJobAsync(jobId, cancellationToken),
                $"Job with id <{jobId}> was already delivered.");
        }

        logger.LogInformation("Declaration for job with id <{JobId}> created.", jobId);
        return new DeliveryDeclarationResult(DeliveryDeclarationStatus.Created, entityEntry.Entity.Id);
    }

    /// <summary>
    /// Creates the transfers of the set the mandate chose. They wait until the uploader can no longer delete the
    /// delivery, and keep the configuration of the set and the values of the mandate as they are at the declaration.
    /// </summary>
    private List<DeliveryReaction> CreateTransfers(Delivery delivery, Mandate mandate)
    {
        if (mandate.ReactionSetId is null)
            return [];

        // The start refuses a mandate with a set while the window never closes, so the window always ends.
        var holdEnd = deliveryOptions.Value.GetDeleteWindowEnd(delivery.Date)
            ?? throw new InvalidOperationException($"Mandate <{mandate.Id}> chose a reaction set while the delete window never closes.");
        var mandateValues = mandate.ReactionSetValues ?? [];

        return pipelineFactory.ResolveTransfers(mandate.ReactionSetId)
            .Select(transfer => new DeliveryReaction
            {
                Delivery = delivery,
                ReactionId = transfer.ReactionId,
                Kind = ReactionKind.Transfer,
                Implementation = transfer.Implementation,
                Config = JsonSerializer.Serialize(new ReactionSnapshot(transfer.Config, mandateValues.GetValueOrDefault(transfer.ReactionId) ?? [])),
                State = ReactionState.Pending,
                NextAttemptAt = holdEnd,
            })
            .ToList();
    }

    private static bool IsJobConflict(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException
            && string.Equals(postgresException.ConstraintName, Context.DeliveryJobIndexName, StringComparison.Ordinal);

    private Task<int?> FindDeliveryOfJobAsync(Guid jobId, CancellationToken cancellationToken)
        => context.Deliveries
            .AsNoTracking()
            .Where(d => d.JobId == jobId)
            .Select(d => (int?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
