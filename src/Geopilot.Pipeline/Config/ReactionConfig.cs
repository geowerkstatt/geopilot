using Geopilot.Pipeline.Validation;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// One reaction of a <see cref="ReactionSetConfig"/>: the channel that runs it, how the reaction overwrites the
/// channel's default configuration and the values the mandate supplies.
/// </summary>
public class ReactionConfig
{
    /// <summary>
    /// What a reaction id and a set id may look like. They end up in the database, at most 128 characters long.
    /// </summary>
    internal const string IdPattern = "^[a-z0-9][a-z0-9_-]{0,127}$";

    /// <summary>
    /// The id of the reaction, unique across all sets.
    /// </summary>
    [YamlMember(Alias = "id")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Reaction ID is required.")]
    [RegularExpression(IdPattern, ErrorMessage = "A reaction id consists of lowercase letters, digits, '_' and '-', starts with a letter or digit and has at most 128 characters.")]
    public required string Id { get; set; }

    /// <summary>
    /// The channel that runs the reaction. References the <see cref="ChannelConfig.Id"/> of a defined channel.
    /// </summary>
    [YamlMember(Alias = "channel_id")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Channel Reference is required.")]
    public string? ChannelId { get; set; }

    /// <summary>
    /// Optional configuration overrides for the channel of this reaction. Overrides the default configuration defined
    /// in <see cref="ChannelConfig.DefaultConfig"/>.
    /// </summary>
    [YamlMember(Alias = "channel_config_overwrites")]
    public Parameterization? ChannelConfigOverwrites { get; set; }

    /// <summary>
    /// The values a mandate supplies to the channel. Only parameters the channel marks as mandate values qualify, and
    /// only those neither the base configuration nor the default configuration of the channel sets.
    /// </summary>
    [YamlMember(Alias = "mandate_config")]
    [NoDuplicates(PropertyName = "Key")]
    public List<MandateConfigEntry>? MandateConfig { get; set; }
}
