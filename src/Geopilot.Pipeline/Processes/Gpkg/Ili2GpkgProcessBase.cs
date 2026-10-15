using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;
using Geowerkstatt.IlitoolsWrapperApi.Ili2gpkg;

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

    /// <summary>
    /// Returns the transfer format based on the file extensions of the provided transfer files.
    /// Normalizes "xml" files to the "xtf" format.
    /// Gml is not recognized as it is only used for export.
    /// </summary>
    /// <param name="transferFiles">The collection of transfer files to determine the format from.</param>
    /// <returns>The normalized transfer format if all files have the same format; otherwise, null.</returns>
    protected static Ili2GpkgTransferFormat? GetFormatOfTransferFiles(IReadOnlyCollection<IPipelineFile> transferFiles)
    {
        static Ili2GpkgTransferFormat? NormalizeExtension(IPipelineFile transferFile)
        {
            return transferFile.FileExtension.ToLowerInvariant() switch
            {
                "xml" or "xtf" => Ili2GpkgTransferFormat.Xtf,
                "itf" => Ili2GpkgTransferFormat.Itf,
                _ => null,
            };
        }

        if (transferFiles.Count == 0)
            return null;

        var extensions = transferFiles.Select(NormalizeExtension).ToHashSet();
        if (extensions.Count == 1)
        {
            return extensions.First();
        }

        return null;
    }
}
