using Geopilot.Api.Services;

namespace Geopilot.Api.Processing;

/// <summary>
/// The wish, expressed when a job starts, that geopilot creates its delivery itself as soon as the run allows it,
/// so an unattended caller does not have to come back for it. Only a machine delivery attempt carries one.
/// </summary>
/// <param name="Supplier">The user or machine client the delivery is created for. It is also the only caller that
/// may read the attempt.</param>
/// <param name="MandateKey">The key of the mandate, echoed in the status of the attempt.</param>
/// <param name="Fields">The delivery fields the caller supplied up front.</param>
public sealed record DeliveryIntent(Supplier Supplier, string MandateKey, DeliveryFields Fields);
