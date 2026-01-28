using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Defines the parameters for a pipeline.
/// </summary>
internal class PipelineParametersConfig
{
    /// <summary>
    /// The name of the upload step in the pipeline. From this attribute the first step will take the data from.
    /// </summary>
    [YamlMember(Alias = "upload_step")]
    [Required(AllowEmptyStrings = false)]
    public required string UploadStep { get; set; }

    /// <summary>
    /// The file mappings for the pipeline.
    /// </summary>
    [YamlMember(Alias = "mapping")]
    [Required]
    public required List<FileMappingsConfig> Mappings { get; set; }
}
