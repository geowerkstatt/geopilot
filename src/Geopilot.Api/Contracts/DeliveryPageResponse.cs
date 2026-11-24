namespace Geopilot.Api.Contracts;

/// <summary>
/// One page of deliveries, newest first.
/// </summary>
/// <param name="TotalCount">How many deliveries match the filters, across all pages.</param>
/// <param name="Items">The deliveries on this page.</param>
public record DeliveryPageResponse(int TotalCount, List<DeliveryResponse> Items);
