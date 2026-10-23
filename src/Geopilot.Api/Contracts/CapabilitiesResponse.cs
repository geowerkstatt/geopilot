namespace Geopilot.Api.Contracts;

/// <summary>
/// The capabilities an installation offers, so the administration only shows what is configured.
/// </summary>
/// <param name="MachineDeliveryEnabled">Whether this installation offers machine based delivery.</param>
/// <param name="ReactionSetsEnabled">Whether a mandate can choose a reaction set, which needs a delete window of the
/// uploaders that closes: the reactions only run once it has.</param>
public record CapabilitiesResponse(bool MachineDeliveryEnabled, bool ReactionSetsEnabled);
