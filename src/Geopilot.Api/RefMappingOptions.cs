namespace Geopilot.Api;

/// <summary>
/// Settings of the reference data mapping that administrators maintain in the admin portal.
/// </summary>
public class RefMappingOptions
{
    /// <summary>
    /// Gets the name of the configuration section that contains the reference data mapping settings.
    /// </summary>
    public static string SectionName => "RefMapping";

    /// <summary>
    /// Whether this installation maintains the reference data mapping in the admin portal. Off unless an
    /// installation asks for it, so installations that name a mapping file in their pipeline keep working as before.
    /// </summary>
    public bool Enabled { get; set; }
}
