namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Marks a constructor parameter of a channel whose value only the base configuration (<c>Pipeline:ChannelConfigs</c>)
/// may set, such as the base directory of a file drop or credentials. A pipeline definition that sets it fails the
/// start, so where a channel writes and with which access stays with the operators of the installation.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class BaseConfigValueAttribute : Attribute
{
}
