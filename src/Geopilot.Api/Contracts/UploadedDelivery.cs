namespace Geopilot.Api.Contracts;

/// <summary>
/// A delivery as the user who declared it sees it in the list of their deliveries. Of the mandate it carries no more
/// than its id and name, because the configuration of a mandate belongs to the administrators.
/// </summary>
/// <param name="Id">The unique identifier of the delivery.</param>
/// <param name="Date">The date the delivery was declared.</param>
/// <param name="Mandate">The mandate the delivery fulfils.</param>
/// <param name="Comment">The comment of the user who declared the delivery.</param>
/// <param name="CanDelete">Whether the user may still delete the delivery.</param>
public record UploadedDelivery(int Id, DateTime Date, MandateReference Mandate, string Comment, bool CanDelete);
