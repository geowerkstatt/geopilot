using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.PipelineCore.Ilitools;

/// <summary>
/// Interface for a client that interacts with the ili2gpkg tool.
/// </summary>
public interface IIli2GpkgClient
{
    /// <summary>
    /// Imports the schema from the INTERLIS model file <paramref name="modelFile"/> into the GeoPackage file <paramref name="gpkgFile"/>.
    /// </summary>
    /// <param name="args">Additional ili2gpkg arguments.</param>
    /// <param name="modelFile">INTERLIS model file.</param>
    /// <param name="gpkgFile">GeoPackage file for the schema import.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An <see cref="Ili2GpkgResult"/> indicating success and the ili2gpkg log content.</returns>
    Task<Ili2GpkgResult> SchemaImportAsync(
        Ili2GpkgArgs args,
        IPipelineFile modelFile,
        IPipelineFile gpkgFile,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports the transfer files from <paramref name="transferFiles"/> into the GeoPackage read from
    /// <paramref name="inputFile"/>. On success, the populated GeoPackage is written to <paramref name="outputFile"/>.
    /// </summary>
    /// <param name="args">Additional ili2gpkg arguments.</param>
    /// <param name="inputFile">Input GeoPackage file.</param>
    /// <param name="outputFile">Output GeoPackage file for a successful import.</param>
    /// <param name="transferFiles">INTERLIS transfer files.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An <see cref="Ili2GpkgResult"/> indicating success and the ili2gpkg log content.</returns>
    Task<Ili2GpkgResult> ImportAsync(
        Ili2GpkgArgs args,
        IPipelineFile inputFile,
        IPipelineFile outputFile,
        IReadOnlyList<IPipelineFile> transferFiles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports the data from the GeoPackage file <paramref name="gpkgFile"/> into the INTERLIS transfer file <paramref name="transferFile"/>.
    /// </summary>
    /// <param name="args">Additional ili2gpkg arguments.</param>
    /// <param name="gpkgFile">GeoPackage file to export data from.</param>
    /// <param name="transferFile">INTERLIS transfer file for the exported data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An <see cref="Ili2GpkgResult"/> indicating success and the ili2gpkg log content.</returns>
    Task<Ili2GpkgResult> ExportAsync(
        Ili2GpkgArgs args,
        IPipelineFile gpkgFile,
        IPipelineFile transferFile,
        CancellationToken cancellationToken = default);
}
