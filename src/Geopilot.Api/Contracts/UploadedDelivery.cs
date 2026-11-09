namespace Geopilot.Api.Contracts;

/// <summary>
/// A delivery as a user sees it in the list of their deliveries: one the user declared, or one of a machine client the
/// user is responsible for. Of the mandate it carries no more than its id and name, because the configuration of a
/// mandate belongs to the administrators.
/// </summary>
/// <param name="Id">The unique identifier of the delivery.</param>
/// <param name="Date">The date the delivery was declared.</param>
/// <param name="Mandate">The mandate the delivery fulfils.</param>
/// <param name="DeclarerName">The name of whoever declared the delivery, the user or the machine client.</param>
/// <param name="Comment">The comment given with the delivery.</param>
/// <param name="CanDelete">Whether the user may still delete the delivery.</param>
public record UploadedDelivery(int Id, DateTime Date, MandateReference Mandate, string DeclarerName, string Comment, bool CanDelete);
