namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Marks a text parameter of a channel whose configured value is the path of a file below the resources directory
/// (<c>Storage:ResourcesDirectory</c>), such as the template of a mail. The channel gets the text of the file, read
/// whenever the channel is constructed, so a changed file applies from the next attempt on.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class ResourceFileAttribute : Attribute
{
}
