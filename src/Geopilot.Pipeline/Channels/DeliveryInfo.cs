namespace Geopilot.Pipeline.Channels;

/// <summary>
/// What a channel learns about the delivery it forwards. Deliberately without personal data.
/// </summary>
/// <param name="Id">The id of the delivery.</param>
/// <param name="JobId">The id of the processing job the delivery comes from.</param>
/// <param name="MandateId">The id of the mandate the delivery fulfils.</param>
/// <param name="DeclaredAt">When the delivery was declared (UTC).</param>
internal sealed record DeliveryInfo(int Id, Guid JobId, int MandateId, DateTime DeclaredAt);
