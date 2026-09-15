namespace Geopilot.Api.Contracts;

/// <summary>
/// The capabilities an installation offers, so the administration only shows what is configured.
/// </summary>
/// <param name="MachineDeliveryEnabled">Whether this installation offers machine based delivery.</param>
public record CapabilitiesResponse(bool MachineDeliveryEnabled);
