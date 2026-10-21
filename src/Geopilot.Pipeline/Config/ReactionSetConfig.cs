using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// A named set of reactions that run once a delivery can no longer be withdrawn (ADR 0015). A mandate chooses one set
/// by its id and fills the values the set asks of it.
/// </summary>
public class ReactionSetConfig
{
    /// <summary>
    /// The id of the set, which a mandate refers to.
    /// </summary>
    [YamlMember(Alias = "id")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Reaction Set ID is required.")]
    [RegularExpression(ReactionConfig.IdPattern, ErrorMessage = "A reaction set id consists of lowercase letters, digits, '_' and '-', starts with a letter or digit and has at most 128 characters.")]
    public required string Id { get; set; }

    /// <summary>
    /// The reactions that forward the delivery. The delivery counts as forwarded once all of them are done.
    /// </summary>
    [YamlMember(Alias = "transfers")]
    [Required(ErrorMessage = "Transfers are required.")]
    [MinLength(1, ErrorMessage = "At least one Transfer is required.")]
    public required List<ReactionConfig> Transfers { get; set; }
}
