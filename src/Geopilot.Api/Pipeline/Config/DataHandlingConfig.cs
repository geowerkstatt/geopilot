using Geopilot.Api.Pipeline.Process;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents the configuration for a Pipeline Process for mapping input and output data fields.
/// </summary>
public class DataHandlingConfig
{
    /// <summary>
    /// Defines how output data fields are mapped. The key is the expected output field name, and the value is the actual data source field name.
    /// </summary>
    [YamlMember(Alias = "output_mapping")]
    public Dictionary<string, string>? OutputMapping { get; set; }

    /// <summary>
    /// Gets the mapped output field name for a given key.
    /// </summary>
    /// <param name="key">The expected output field name.</param>
    /// <returns>The actual data source field name.</returns>
    public string GetOutputMapping(string key)
    {
        if (OutputMapping != null && OutputMapping.TryGetValue(key, out var mappedKey))
        {
            return mappedKey;
        }

        throw new KeyNotFoundException($"output mapping for key '{key}' not found.");
    }
}
