namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Forwards a delivery that can no longer be withdrawn (ADR 0015), one attempt per call. A channel runs again after a
/// failed attempt and after an attempt whose instance stopped, so it writes in a way that a repeated attempt neither
/// fails on nor duplicates what an earlier one left behind, and it removes what it started when it fails. Its
/// constructor only checks and keeps its values: geopilot also constructs a channel at the start and to check the
/// values of a mandate, and discards it again.
/// </summary>
internal interface IDeliveryChannel
{
    /// <summary>
    /// Forwards the delivery.
    /// </summary>
    /// <param name="delivery">What the channel may know about the delivery.</param>
    /// <param name="files">The delivered files, readable only.</param>
    /// <param name="cancellationToken">Cancelled when the attempt runs out of time or the application stops.</param>
    /// <returns>Whether the delivery was forwarded, with a message for the operators.</returns>
    Task<ChannelResult> DeliverAsync(DeliveryInfo delivery, IReadOnlyList<IDeliveryFile> files, CancellationToken cancellationToken);
}
