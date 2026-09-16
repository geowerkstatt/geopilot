using Geopilot.Api.Services;

namespace Geopilot.Api.Processing;

/// <summary>
/// Declares the delivery of a machine delivery attempt once its run finished, so an unattended caller does not
/// have to come back for it. Registered only while machine delivery is enabled.
/// </summary>
public class MachineDeliveryCompletionHandler : IJobCompletionHandler
{
    private readonly ILogger<MachineDeliveryCompletionHandler> logger;
    private readonly ISubmissionStore submissionStore;
    private readonly IDeliveryDeclarationService declarationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MachineDeliveryCompletionHandler"/> class.
    /// </summary>
    public MachineDeliveryCompletionHandler(
        ILogger<MachineDeliveryCompletionHandler> logger,
        ISubmissionStore submissionStore,
        IDeliveryDeclarationService declarationService)
    {
        this.logger = logger;
        this.submissionStore = submissionStore;
        this.declarationService = declarationService;
    }

    /// <inheritdoc/>
    public async Task OnJobFinishedAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var submission = submissionStore.GetSubmission(jobId);
        if (submission is null)
        {
            // Not a machine delivery: a job started from the web interface declares its delivery itself.
            return;
        }

        DeliveryDeclarationResult result;
        try
        {
            result = await declarationService.DeclareAsync(jobId, submission.DeliveryFields, submission.Declarer, cancellationToken);
        }
        catch (Exception ex)
        {
            // Nothing else answers the caller: the attempt is polled, and letting this escape would leave it
            // reporting a delivery still in progress until the job is retired a day later.
            submissionStore.MarkDeclarationFailed(jobId, "The delivery could not be declared because of an unexpected error.");
            logger.LogError(ex, "Declaring the delivery for job <{JobId}> failed unexpectedly.", jobId);
            return;
        }

        switch (result.Status)
        {
            case DeliveryDeclarationStatus.Created when result.DeliveryId is int deliveryId:
                submissionStore.MarkDelivered(jobId, deliveryId);
                logger.LogInformation("Declared delivery <{DeliveryId}> for machine delivery of job <{JobId}>.", deliveryId, jobId);
                break;

            case DeliveryDeclarationStatus.AlreadyDeclared when result.DeliveryId is int existingDeliveryId:
                // The caller declared the delivery itself before the run finished. It exists, so reporting the
                // attempt as failed would contradict it.
                submissionStore.MarkDelivered(jobId, existingDeliveryId);
                logger.LogInformation("Job <{JobId}> was already delivered as <{DeliveryId}>, so the attempt adopts it.", jobId, existingDeliveryId);
                break;

            case DeliveryDeclarationStatus.JobNotDeliverable:
                // The expected outcome for data that did not pass. The run's own state already says so, and
                // the attempt reports it as rejected rather than as a failed declaration.
                logger.LogInformation("Job <{JobId}> did not allow a delivery, so none was declared.", jobId);
                break;

            default:
                submissionStore.MarkDeclarationFailed(jobId, DescribeFailure(result));
                logger.LogWarning("Could not declare the delivery for job <{JobId}>: {Status}.", jobId, result.Status);
                break;
        }
    }

    /// <summary>
    /// Turns a refused declaration into the one sentence the caller gets to read. Violated field rules carry
    /// their detail in <see cref="DeliveryDeclarationResult.FieldErrors"/> rather than in the message, and the
    /// status alone would not say which field the caller has to correct.
    /// </summary>
    private static string DescribeFailure(DeliveryDeclarationResult result)
    {
        if (result.FieldErrors is not { Count: > 0 })
            return result.Message ?? $"The delivery was refused ({result.Status}).";

        var violations = result.FieldErrors.Select(error => $"{error.Key}: {string.Join(" ", error.Value)}");
        return $"The delivery details do not satisfy the mandate. {string.Join(" ", violations)}";
    }
}
