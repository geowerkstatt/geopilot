using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Configuration for file mappings in a pipeline.
/// </summary>
public class FileMappingsConfig
{
    /// <summary>
    /// The file extension to match for this mapping.
    /// </summary>
    [YamlMember(Alias = "file_extension")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Pipeline Parameter File Extension is required.")]
    [RegularExpression("^[a-zA-Z]{2,}$", ErrorMessage = "invalid file extension")]
    public required string FileExtension { get; set; }

    /// <summary>
    /// The attribute to map the file to.
    /// </summary>
    [YamlMember(Alias = "attribute")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Pipeline Parameter File Attribute is required.")]
    public required string Attribute { get; set; }
}
