using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Configuration for input data handling in a pipeline step.
/// </summary>
internal class InputConfig
{
    /// <summary>
    /// The name of the step from which to take the data from.
    /// </summary>
    [YamlMember(Alias = "from")]
    [Required(AllowEmptyStrings = false)]
    public required string From { get; set; }

    /// <summary>
    /// The name of the attribute from which to take the data.
    /// </summary>
    [YamlMember(Alias = "take")]
    [Required(AllowEmptyStrings = false)]
    public required string Take { get; set; }

    /// <summary>
    /// The attribute name to map the input data to.
    /// </summary>
    [YamlMember(Alias = "as")]
    [Required(AllowEmptyStrings = false)]
    public required string As { get; set; }
}
