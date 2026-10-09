using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline.Ilitools;

/// <summary>
/// Client of the XTF-Diff-Tool, which lists the changes between an old and a new state of an INTERLIS transfer file.
/// Internal, because only the built-in diff process uses it; unlike the clients of the ilitools it is no plugin contract.
/// </summary>
internal interface IXtfDiffClient
{
    /// <summary>
    /// Compares <paramref name="oldTransferFile"/> with <paramref name="newTransferFile"/>: an object only the old state
    /// contains counts as deleted, one only the new state contains as added. The tool writes its log to
    /// <paramref name="logFile"/> in any case and, if it compared the two states, the changes as a JSON array to
    /// <paramref name="diffFile"/>.
    /// </summary>
    /// <param name="modelDirs">
    /// The INTERLIS model repositories the models are resolved from, searched in the given order: <c>http(s)</c> URLs and
    /// repositories the wrapper offers as <c>%REPOSITORIES/&lt;id&gt;</c>. The tool knows no placeholder of its own and
    /// takes no models from the request. <see langword="null"/> leaves the default of the tool, models.interlis.ch.
    /// </param>
    /// <param name="oldTransferFile">The earlier state of the transfer file.</param>
    /// <param name="newTransferFile">The later state of the transfer file.</param>
    /// <param name="logFile">File to write the log of the tool to.</param>
    /// <param name="diffFile">File to write the changes to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the tool compared the two states.</returns>
    Task<bool> DiffAsync(
        IReadOnlyList<string>? modelDirs,
        IPipelineFile oldTransferFile,
        IPipelineFile newTransferFile,
        IPipelineFile logFile,
        IPipelineFile diffFile,
        CancellationToken cancellationToken);
}
