using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Configuration for input data handling in a pipeline step.
/// </summary>
public class InputConfig
{
    /// <summary>
    /// The name of the step from which to take the data from.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Input from is required.")]
    [YamlMember(Alias = "from")]
    public required string From { get; set; }

    /// <summary>
    /// The name of the attribute from which to take the data.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Input take is required.")]
    [YamlMember(Alias = "take")]
    public required string Take { get; set; }

    /// <summary>
    /// The attribute name to map the input data to.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Input as is required.")]
    [YamlMember(Alias = "as")]
    public required string As { get; set; }
}
