using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Defines the parameters for a pipeline.
/// </summary>
public class PipelineParametersConfig
{
    /// <summary>
    /// The name of the upload step in the pipeline. From this attribute the first step will take the data from.
    /// </summary>
    [YamlMember(Alias = "upload_step")]
    [Required(AllowEmptyStrings = false, ErrorMessage = "Pipeline Parameter Upload Step is required.")]
    public required string UploadStep { get; set; }

    /// <summary>
    /// The file mappings for the pipeline.
    /// </summary>
    [YamlMember(Alias = "mapping")]
    [Required(ErrorMessage = "Pipeline Parameter Mappings is required.")]
    [MinLength(1, ErrorMessage = "At least one Mapping is required.")]
    public required List<FileMappingsConfig> Mappings { get; set; }
}
