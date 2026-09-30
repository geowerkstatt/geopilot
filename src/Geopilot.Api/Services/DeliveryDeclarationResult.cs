namespace Geopilot.Api.Services;

/// <summary>
/// The outcome of a delivery declaration. Deliberately not an HTTP result: the declaration runs both from a
/// controller and from the processing runner, and those report a failure in different ways.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="DeliveryId">The delivery of the job: the one just created for <see cref="DeliveryDeclarationStatus.Created"/>,
/// the one that was already there for <see cref="DeliveryDeclarationStatus.AlreadyDeclared"/>, and null otherwise.</param>
/// <param name="Message">A message describing the outcome, meant for the caller.</param>
/// <param name="FieldErrors">The violated field rules keyed by field name, set only for
/// <see cref="DeliveryDeclarationStatus.FieldRulesViolated"/>.</param>
public record DeliveryDeclarationResult(
    DeliveryDeclarationStatus Status,
    int? DeliveryId = null,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null);

/// <summary>
/// The possible outcomes of a delivery declaration.
/// </summary>
public enum DeliveryDeclarationStatus
{
    /// <summary>The delivery was created.</summary>
    Created,

    /// <summary>No job is known under the given id.</summary>
    JobNotFound,

    /// <summary>The job has not completed, or its run does not allow a delivery.</summary>
    JobNotDeliverable,

    /// <summary>The job was started without a mandate and can therefore not be delivered.</summary>
    JobWithoutMandate,

    /// <summary>The job was already delivered. A job yields at most one delivery.</summary>
    AlreadyDeclared,

    /// <summary>The mandate is not accessible for the declaring user, or does not accept deliveries.</summary>
    MandateNotDeliverable,

    /// <summary>The delivery fields violate the rules the mandate configures.</summary>
    FieldRulesViolated,

    /// <summary>The assets of the job could not be persisted.</summary>
    AssetRecordingFailed,

    /// <summary>The job produced no assets, so there is nothing to deliver.</summary>
    NoAssets,
}
