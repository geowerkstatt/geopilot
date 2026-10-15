using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Gpkg;

internal abstract class Ili2GpkgProcessBase(
    string? metaConfig,
    string? modelDirs,
    string? toolVersion,
    string? pluginIds)
{
    private const string IlidataPrefix = "ilidata:";

    protected Ili2GpkgArgs CreateCommonArgs()
    {
        return new Ili2GpkgArgs
        {
            MetaConfig = ToIlidataReference(metaConfig),
            ModelDirs = SplitConfiguredList(modelDirs),
            ToolVersion = string.IsNullOrWhiteSpace(toolVersion) ? null : toolVersion.Trim(),
            PluginIds = SplitConfiguredList(pluginIds),
        };
    }

    /// <summary>
    /// Splits a hand written, semicolon separated configuration value, dropping blanks around the entries and
    /// empty entries. Returns null if nothing is left, so the tool keeps its own default.
    /// </summary>
    private static string[]? SplitConfiguredList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var entries = value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return entries.Length > 0 ? entries : null;
    }

    protected static string? ToIlidataReference(string? datasetId)
    {
        datasetId = datasetId?.Trim();
        if (string.IsNullOrEmpty(datasetId))
            return null;

        // The tool resolves the profile through the repository index, so the configured value is a dataset id.
        return datasetId.StartsWith(IlidataPrefix, StringComparison.OrdinalIgnoreCase)
            ? datasetId
            : IlidataPrefix + datasetId;
    }
}
