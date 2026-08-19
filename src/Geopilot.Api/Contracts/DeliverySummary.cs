namespace Geopilot.Api.Contracts;

/// <summary>
/// Represents a summary of a delivery.
/// </summary>
/// <param name="Id">The unique identifier of the delivery.</param>
/// <param name="Date">The date the delivery was declared.</param>
public record DeliverySummary(int Id, DateTime Date);
