using Geopilot.Api.Pipeline.Process;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline.Config;

/// <summary>
/// Represents the configuration for a Pipeline Process for mapping input and output data fields.
/// </summary>
public class DataHandlingConfig
{
    /// <summary>
    /// Defines how input data fields are mapped. The key is the expected input field name, and the value is the actual data source field name.
    /// </summary>
    [YamlMember(Alias = "input_mapping")]
    public Dictionary<string, string>? InputMapping { get; set; }

    /// <summary>
    /// Defines how output data fields are mapped. The key is the expected output field name, and the value is the actual data source field name.
    /// </summary>
    [YamlMember(Alias = "output_mapping")]
    public Dictionary<string, string>? OutputMapping { get; set; }

    /// <summary>
    /// Gets the mapped input field name for a given key.
    /// </summary>
    /// <param name="key">The expected input field name.</param>
    /// <returns>The actual data source field name.</returns>
    public string GetInputMapping(string key)
    {
        if (InputMapping != null && InputMapping.TryGetValue(key, out var mappedKey))
        {
            return mappedKey;
        }

        throw new KeyNotFoundException($"input mapping for key '{key}' not found.");
    }

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
