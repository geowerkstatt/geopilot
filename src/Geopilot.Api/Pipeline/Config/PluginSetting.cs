namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// application settings for a pipeline processor plugin. Defined in the application settings under 'Pipeline:Plugins'.
/// </summary>
public class PluginSetting
{
    /// <summary>
    /// Gets or sets the list of package names for the plugin.
    /// </summary>
    public required HashSet<string> Packagenames { get; set; }

    /// <summary>
    /// Gets or sets the assembly file for the plugin.
    /// </summary>
    public required string AssemblyFile { get; set; }
}
