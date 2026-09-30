using Geopilot.Api.Services;

namespace Geopilot.Api.Processing;

/// <summary>
/// A machine delivery attempt: what the caller asked for, kept until the run finishes and the delivery is
/// declared on its behalf. Keyed by the processing job, which is also the id the caller polls.
/// </summary>
/// <param name="JobId">The processing job that runs for this attempt.</param>
/// <param name="MandateKey">The key the caller addressed the mandate by, echoed in the status.</param>
/// <param name="Declarer">The user or machine client the delivery is declared for.</param>
/// <param name="DeliveryFields">The delivery fields the caller supplied up front.</param>
public record Submission(Guid JobId, string MandateKey, Declarer Declarer, DeliveryFields DeliveryFields)
{
    /// <summary>
    /// The delivery that was declared for this attempt, once it exists.
    /// </summary>
    public int? DeliveryId { get; init; }

    /// <summary>
    /// Why the delivery could not be declared, although the run allowed it. Set instead of
    /// <see cref="DeliveryId"/>, never next to it.
    /// </summary>
    public string? DeclarationFailure { get; init; }
}
