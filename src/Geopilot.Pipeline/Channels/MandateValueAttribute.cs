namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Marks a constructor parameter of a channel whose value the mandate supplies, such as the subfolder of a file drop.
/// A mandate fills marked parameters only, and only those its set declares in <c>mandate_config</c>. Every other
/// parameter comes from the host (<c>Pipeline:ChannelConfigs</c>), so a mandate can never set a base directory or
/// credentials.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class MandateValueAttribute : Attribute
{
}
