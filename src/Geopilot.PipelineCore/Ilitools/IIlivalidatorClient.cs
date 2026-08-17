using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.PipelineCore.Ilitools;

/// <summary>
/// Interface for a client that interacts with the ilivalidator tool.
/// </summary>
public interface IIlivalidatorClient
{
    /// <summary>
    /// Validates the INTERLIS transfer file <paramref name="transferFile"/> against its models. The tool writes its
    /// own log to <paramref name="logFile"/> and the errors as an INTERLIS transfer file to
    /// <paramref name="xtfLogFile"/>; both are written whether the validation succeeds or fails.
    /// </summary>
    /// <param name="args">Additional ilivalidator arguments.</param>
    /// <param name="transferFile">INTERLIS transfer file to validate.</param>
    /// <param name="logFile">File to write the ilivalidator log to.</param>
    /// <param name="xtfLogFile">File to write the XTF validation log to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An <see cref="IlivalidatorResult"/> indicating whether the validation succeeded.</returns>
    Task<IlivalidatorResult> ValidateAsync(
        IlivalidatorArgs args,
        IPipelineFile transferFile,
        IPipelineFile logFile,
        IPipelineFile xtfLogFile,
        CancellationToken cancellationToken = default);
}
