namespace Geopilot.Pipeline.Channels;

/// <summary>
/// The value of one channel parameter as an attempt ran with it, kept in the execution protocol.
/// </summary>
/// <param name="Value">The value as configured, not converted; a secret that is set reads <c>**********</c>, and for
/// a resource file it is the path below the resources directory.</param>
/// <param name="Origin">Where the value came from.</param>
/// <param name="Sha256">For a resource file the SHA-256 of its content when the attempt started, as lowercase hex;
/// <see langword="null"/> when the file is missing or lies outside the resources directory, and for any other
/// parameter.</param>
public sealed record RecordedConfigValue(object? Value, ConfigOrigin Origin, string? Sha256 = null);

/// <summary>
/// Where the value of a channel parameter came from for an attempt.
/// </summary>
public enum ConfigOrigin
{
    /// <summary>The base configuration of the installation, <c>Pipeline:ChannelConfigs</c>.</summary>
    Base,

    /// <summary>The pipeline definition: the <c>default_config</c> of the channel with the reaction's
    /// <c>channel_config_overwrites</c>, frozen when the delivery was declared.</summary>
    Definition,

    /// <summary>The mandate, for a parameter marked as mandate value.</summary>
    Mandate,
}
