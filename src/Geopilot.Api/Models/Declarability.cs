namespace Geopilot.Api.Models;

/// <summary>
/// Whether whoever started a run may deliver to its mandate, regardless of whether its data allow a delivery. Decided once when its job starts. Persisted as text.
/// </summary>
public enum Declarability
{
    /// <summary>Whoever started the run is a member of an organisation of the mandate, and the mandate accepts deliveries.</summary>
    Declarable,

    /// <summary>Whoever started the run is no member of an organisation of the mandate (an anonymous start never is), or the mandate accepts no deliveries.</summary>
    NotDeclarable,
}
