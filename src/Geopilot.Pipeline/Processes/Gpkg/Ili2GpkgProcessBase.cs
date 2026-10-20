using Geopilot.Pipeline.Ilitools;
using Geopilot.PipelineCore.Ilitools;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Processes.Gpkg;

internal abstract class Ili2GpkgProcessBase(
    string? metaConfig,
    string? modelDirs,
    string? toolVersion,
    string? pluginIds)
{
    protected Ili2GpkgArgs CreateCommonArgs()
    {
        return new Ili2GpkgArgs
        {
            MetaConfig = IlitoolsHelper.ToIlidataReference(metaConfig),
            ModelDirs = IlitoolsHelper.SplitConfiguredList(modelDirs),
            ToolVersion = string.IsNullOrWhiteSpace(toolVersion) ? null : toolVersion.Trim(),
            PluginIds = IlitoolsHelper.SplitConfiguredList(pluginIds),
        };
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
