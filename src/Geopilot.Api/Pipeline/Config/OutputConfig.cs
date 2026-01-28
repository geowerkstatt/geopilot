using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Configuration for output data handling in a pipeline step.
/// </summary>
internal class OutputConfig
{
    /// <summary>
    /// The attribute name to take the data from.
    /// </summary>
    [YamlMember(Alias = "take")]
    [Required(AllowEmptyStrings = false)]
    public string? Take { get; set; }

    /// <summary>
    /// The attribute name to map the output data to.
    /// </summary>
    [YamlMember(Alias = "as")]
    [Required(AllowEmptyStrings = false)]
    public string? As { get; set; }

    /// <summary>
    /// The action to perform with the output data.
    /// </summary>
    [YamlMember(Alias = "action")]
    [Required(AllowEmptyStrings = false)]
    public OutputAction? Action { get; set; }
}
