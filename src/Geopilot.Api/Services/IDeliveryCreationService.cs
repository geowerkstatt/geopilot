using Geopilot.Api.Models;
using System.Text.Json;

namespace Geopilot.Api.Services;

/// <summary>
/// Turns a processed job into a <see cref="Delivery"/>. Holds the rules that decide whether a job may be
/// delivered at all, independent of how the request reached the application.
/// </summary>
public interface IDeliveryCreationService
{
    /// <summary>
    /// Creates the delivery for the specified job.
    /// </summary>
    /// <param name="jobId">The processing job the delivery is created for.</param>
    /// <param name="fields">The delivery fields whose evaluation the mandate configures.</param>
    /// <param name="supplier">The user or machine client the delivery is created for. Ids and not entities,
    /// because the creation also runs outside a request, where an entity loaded earlier belongs to a context
    /// that is long gone.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome of the creation.</returns>
    Task<DeliveryCreationResult> CreateAsync(Guid jobId, DeliveryFields fields, Supplier supplier, CancellationToken cancellationToken);
}

/// <summary>
/// The delivery fields whose evaluation each mandate configures individually.
/// </summary>
/// <param name="PartialDelivery">Whether the delivery covers only part of the mandate.</param>
/// <param name="PrecursorDeliveryId">The delivery this one supersedes.</param>
/// <param name="Comment">The comment accompanying the delivery.</param>
/// <param name="Metadata">The values of the mandate's metadata fields, keyed by field id; <see langword="null"/> when none were supplied.</param>
/// <param name="ClientReference">The caller's own identifier of the delivery, already trimmed, which the protocol records
/// and the status of the attempt echoes. No mandate evaluates it.</param>
public record DeliveryFields(bool? PartialDelivery, int? PrecursorDeliveryId, string? Comment, IReadOnlyDictionary<string, JsonElement>? Metadata = null, string? ClientReference = null);
