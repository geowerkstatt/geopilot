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
    public required string FileExtension { get; set; }

    /// <summary>
    /// The attribute to map the file to.
    /// </summary>
    [YamlMember(Alias = "attribute")]
    public required string Attribute { get; set; }
}
