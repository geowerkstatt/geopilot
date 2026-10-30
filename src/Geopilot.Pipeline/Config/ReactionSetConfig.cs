using Geopilot.Pipeline.Validation;
using Geopilot.PipelineCore.Pipeline;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// A named set of reactions that run once a delivery can no longer be withdrawn (ADR 0015). A mandate chooses one set
/// by its id and fills the values the set asks of it.
/// </summary>
[AtLeastOneReaction]
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
    /// The name under which the administration offers the set to a mandate.
    /// </summary>
    [YamlMember(Alias = "display_name")]
    [Required(ErrorMessage = "Reaction Set Display Name is required.")]
    public required LocalizedText DisplayName { get; set; }

    /// <summary>
    /// The reactions that forward the delivery. The delivery counts as forwarded once all of them are done, and right
    /// away when the set has none.
    /// </summary>
    [YamlMember(Alias = "transfers")]
    public List<ReactionConfig>? Transfers { get; set; }

    /// <summary>
    /// The reactions that report on the delivery, such as a mail to the recipients the mandate names. They run once the
    /// delivery is forwarded and never change its state.
    /// </summary>
    [YamlMember(Alias = "notifications")]
    public List<ReactionConfig>? Notifications { get; set; }

    /// <summary>
    /// The transfers and then the notifications of the set.
    /// </summary>
    internal IEnumerable<ReactionConfig> AllReactions() => (Transfers ?? []).Concat(Notifications ?? []);
}
