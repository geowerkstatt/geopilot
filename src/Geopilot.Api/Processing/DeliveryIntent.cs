using Geopilot.Api.Services;

namespace Geopilot.Api.Processing;

/// <summary>
/// The wish, expressed when a job starts, that geopilot declares its delivery itself as soon as the run allows it,
/// so an unattended caller does not have to come back for it. Only a machine delivery attempt carries one.
/// </summary>
/// <param name="Declarer">The user or machine client the delivery is declared for. It is also the only caller that
/// may read the attempt.</param>
/// <param name="MandateKey">The key of the mandate, echoed in the status of the attempt.</param>
/// <param name="Fields">The delivery fields the caller supplied up front.</param>
/// <param name="ClientReference">The caller's own identifier of the delivery, recorded with the run and echoed in the
/// status of the attempt, or <see langword="null"/> when the attempt carries none.</param>
public sealed record DeliveryIntent(Declarer Declarer, string MandateKey, DeliveryFields Fields, string? ClientReference = null);
