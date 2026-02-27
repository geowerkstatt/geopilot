using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Interface for a pipeline.
/// </summary>
public interface IPipeline : IDisposable
{
    /// <summary>
    /// The unique id of the pipeline.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// A human-readable display name for the pipeline in different languages. Key: ISO 639 language code, Value: The display name for that language.
    /// </summary>
    Dictionary<string, string> DisplayName { get; }

    /// <summary>
    /// The parameters for the pipeline.
    /// </summary>
    PipelineParametersConfig Parameters { get; }

    /// <summary>
    /// The steps in the pipeline to be executed sequentially.
    /// </summary>
    List<IPipelineStep> Steps { get; }

    /// <summary>
    /// The current state of the pipeline.
    /// </summary>
    PipelineState State { get; }

    /// <summary>
    /// Runs the pipeline with the specified input file.
    /// </summary>
    /// <param name="file">The input file to process.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the pipeline run.</param>
    /// <returns>The context containing the results of the pipeline execution.</returns>
    /// <exception cref="PipelineRunException">The pipeline run failed due to a misconfigured pipeline or an misbehaving process.</exception>
    /// <exception cref="OperationCanceledException">The pipeline run was cancelled.</exception>
    Task<PipelineContext> Run(IPipelineTransferFile file, CancellationToken cancellationToken);
}
