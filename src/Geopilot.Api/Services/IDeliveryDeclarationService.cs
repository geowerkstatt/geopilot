using Geopilot.Api.Models;

namespace Geopilot.Api.Services;

/// <summary>
/// Turns a processed job into a <see cref="Delivery"/>. Holds the rules that decide whether a job may be
/// delivered at all, independent of how the request reached the application.
/// </summary>
public interface IDeliveryDeclarationService
{
    /// <summary>
    /// Declares the delivery for the specified job.
    /// </summary>
    /// <param name="jobId">The processing job the delivery is declared for.</param>
    /// <param name="fields">The delivery fields whose evaluation the mandate configures.</param>
    /// <param name="declaringUserId">The user the delivery is declared for. Passed as an id and not as an entity,
    /// because the declaration also runs outside a request, where an entity loaded earlier belongs to a context
    /// that is long gone.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome of the declaration.</returns>
    Task<DeliveryDeclarationResult> DeclareAsync(Guid jobId, DeliveryFields fields, int declaringUserId, CancellationToken cancellationToken);
}

/// <summary>
/// The delivery fields whose evaluation each mandate configures individually.
/// </summary>
/// <param name="PartialDelivery">Whether the delivery covers only part of the mandate.</param>
/// <param name="PrecursorDeliveryId">The delivery this one supersedes.</param>
/// <param name="Comment">The comment accompanying the delivery.</param>
public record DeliveryFields(bool? PartialDelivery, int? PrecursorDeliveryId, string? Comment);
