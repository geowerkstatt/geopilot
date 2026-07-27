using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// Assigns one or more <see cref="OutputAction"/>s to a single result property of a step's process.
/// </summary>
public class OutputActionConfig
{
    /// <summary>
    /// The name of the process result property (PascalCase) the actions apply to.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Output action property is required.")]
    [YamlMember(Alias = "property")]
    public required string Property { get; set; }

    /// <summary>
    /// The actions to perform on the referenced output. At least one action is required.
    /// </summary>
    [Required(ErrorMessage = "Output action actions are required.")]
    [MinLength(1, ErrorMessage = "At least one action is required.")]
    [YamlMember(Alias = "actions")]
    public required HashSet<OutputAction> Actions { get; set; }
}
