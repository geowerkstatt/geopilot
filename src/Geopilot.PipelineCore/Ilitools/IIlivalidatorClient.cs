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
    /// <param name="modelRepositoryArchive">
    /// Optional ZIP archive of an INTERLIS model repository. The service unpacks it into its own subfolder of the
    /// session, which <see cref="IlivalidatorArgs.ModelDirs"/> reaches through the entry <c>%ITF_DIR/repository</c>.
    /// This is how a repository that is not published can be used, and its content is trusted as configuration: it
    /// can define models, carry a validation profile and point at further repositories.
    /// </param>
    /// <param name="modelFiles">
    /// Optional single INTERLIS model files (<c>.ili</c>) delivered alongside the transfer file. The service stores
    /// them in their own subfolder of the session under names of its own, so a repository index (<c>ilidata.xml</c>,
    /// <c>ilisite.xml</c>, <c>ilimodels.xml</c>) cannot be smuggled in, which makes this the channel for models from
    /// an unreviewed source such as an upload. They are visible only through the entry <c>%ITF_DIR/models</c> in
    /// <see cref="IlivalidatorArgs.ModelDirs"/>; its position decides the precedence against the other sources,
    /// unreviewed content belongs at the end.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An <see cref="IlivalidatorResult"/> indicating whether the validation succeeded.</returns>
    Task<IlivalidatorResult> ValidateAsync(
        IlivalidatorArgs args,
        IPipelineFile transferFile,
        IPipelineFile logFile,
        IPipelineFile xtfLogFile,
        IPipelineFile? modelRepositoryArchive = null,
        IReadOnlyList<IPipelineFile>? modelFiles = null,
        CancellationToken cancellationToken = default);
}
