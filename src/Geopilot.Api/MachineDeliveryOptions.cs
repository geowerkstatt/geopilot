namespace Geopilot.Api;

/// <summary>
/// Settings of machine based delivery: uploading and delivering data through the API instead of
/// through the web interface.
/// </summary>
public class MachineDeliveryOptions
{
    /// <summary>
    /// Gets the name of the configuration section that contains the machine delivery settings.
    /// </summary>
    public static string SectionName => "MachineDelivery";

    /// <summary>
    /// Whether this installation offers machine based delivery. Off unless an installation asks for it:
    /// it is a capability an operator buys, and while it is off the administration does not offer the
    /// mandate keys that only machine clients use, so nobody maintains a value that means nothing to them.
    /// </summary>
    public bool Enabled { get; set; }
}
