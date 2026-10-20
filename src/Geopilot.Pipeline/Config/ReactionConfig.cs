using Geopilot.Pipeline.Validation;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// One reaction of a <see cref="DeliveryProcessingConfig"/>: the channel that runs it and the values the mandate
/// supplies to that channel.
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
    /// The fully qualified type name of the channel that runs the reaction.
    /// </summary>
    [YamlMember(Alias = "implementation")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Reaction Implementation is required.")]
    public required string Implementation { get; set; }

    /// <summary>
    /// The values a mandate supplies to the channel. Only parameters the channel marks as mandate values qualify.
    /// </summary>
    [YamlMember(Alias = "mandate_config")]
    [NoDuplicates(PropertyName = "Key")]
    public List<MandateConfigEntry>? MandateConfig { get; set; }
}
