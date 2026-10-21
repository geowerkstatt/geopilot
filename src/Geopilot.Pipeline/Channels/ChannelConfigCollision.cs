namespace Geopilot.Pipeline.Channels;

/// <summary>
/// Formats the diagnostics for a channel configuration key that the pipeline definition sets although the hosting
/// base configuration (<c>Pipeline:ChannelConfigs</c>) already pins it, worded like the process counterpart
/// <see cref="Process.ProcessConfigCollision"/>.
/// </summary>
internal static class ChannelConfigCollision
{
    private const string Remedy =
        "The base configuration cannot be overridden. Remove the key from the pipeline definition, " +
        "or remove it from the base configuration if the value has to be set per reaction or per mandate.";

    internal static string InDefaultConfig(string implementation, string channelId, string key) =>
        $"{Preamble(implementation, key)} and in the pipeline definition " +
        $"('channels[id={channelId}].default_config.{key}'). {Remedy}";

    internal static string InReactionOverwrite(string implementation, string setId, string reactionId, string key) =>
        $"{Preamble(implementation, key)} and overwritten in the pipeline definition " +
        $"('reaction_sets[id={setId}].transfers[id={reactionId}].channel_config_overwrites.{key}'). {Remedy}";

    internal static string InMandateConfig(string implementation, string setId, string reactionId, string key) =>
        $"{Preamble(implementation, key)} and declared as a mandate value in the pipeline definition " +
        $"('reaction_sets[id={setId}].transfers[id={reactionId}].mandate_config[key={key}]'). {Remedy}";

    private static string Preamble(string implementation, string key) =>
        $"Channel configuration collision for implementation '{implementation}': the key '{key}' is set " +
        $"in the base configuration (app settings 'Pipeline:ChannelConfigs:{implementation}:{key}', " +
        $"environment variable 'Pipeline__ChannelConfigs__{implementation}__{key}')";
}
