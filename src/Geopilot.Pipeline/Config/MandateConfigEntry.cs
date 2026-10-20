using Geopilot.PipelineCore.Pipeline;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Pipeline.Config;

/// <summary>
/// A value a reaction asks of the mandate, such as the subfolder of a file drop. The administration shows one field
/// per entry.
/// </summary>
public class MandateConfigEntry
{
    /// <summary>
    /// The name of the channel parameter that takes the value.
    /// </summary>
    [YamlMember(Alias = "key")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Mandate Config Key is required.")]
    public required string Key { get; set; }

    /// <summary>
    /// The label of the field in the administration.
    /// </summary>
    [YamlMember(Alias = "display_name")]
    [Required(ErrorMessage = "Mandate Config Display Name is required.")]
    public required LocalizedText DisplayName { get; set; }
}
