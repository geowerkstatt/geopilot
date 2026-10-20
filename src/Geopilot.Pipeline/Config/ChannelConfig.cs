using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// A channel the reactions of <c>delivery_processing</c> reference by its id, as steps reference a
/// <see cref="ProcessConfig"/>: its implementation and the configuration every reaction on it starts from.
/// </summary>
public class ChannelConfig
{
    /// <summary>
    /// The unique channel identifier. Used to reference this channel in <see cref="ReactionConfig"/>.
    /// </summary>
    [YamlMember(Alias = "id")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Channel ID is required.")]
    public required string Id { get; set; }

    /// <summary>
    /// The fully qualified type name of the channel.
    /// </summary>
    [YamlMember(Alias = "implementation")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Channel Implementation is required.")]
    public required string Implementation { get; set; }

    /// <summary>
    /// Optional default configuration for the channel. Can be overridden by <see cref="ReactionConfig.ChannelConfigOverwrites"/>.
    /// </summary>
    [YamlMember(Alias = "default_config")]
    public Parameterization? DefaultConfig { get; set; }
}
