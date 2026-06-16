using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Pipeline;

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
    /// The pipeline's localized display name.
    /// </summary>
    LocalizedText DisplayName { get; }

    /// <summary>
    /// The steps in the pipeline to be executed sequentially.
    /// </summary>
    List<IPipelineStep> Steps { get; }

    /// <summary>
    /// The current state of the pipeline.
    /// </summary>
    ProcessingState State { get; }

    /// <summary>
    /// Gets the pipeline delivery configuration used for processing messages.
    /// </summary>
    PipelineDelivery Delivery { get; }

    /// <summary>
    /// The localized message describing why delivery is prevented, or <see langword="null"/> when delivery is allowed.
    /// Set when matched delivery restrictions are evaluated; <see langword="null"/> also when delivery is prevented
    /// for reasons other than restriction matches (e.g. pipeline failure).
    /// </summary>
    LocalizedText? DeliveryRestrictionMessage { get; }

    /// <summary>
    /// The unique identifier for the job associated with this pipeline execution, used for logging and tracking purposes.
    /// </summary>
    Guid JobId { get; }

    /// <summary>
    /// Runs the pipeline with the specified input file.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the pipeline run.</param>
    /// <returns>The context containing the results of the pipeline execution.</returns>
    /// <exception cref="PipelineRunException">The pipeline run failed due to a misconfigured pipeline or an misbehaving process.</exception>
    /// <exception cref="OperationCanceledException">The pipeline run was cancelled.</exception>
    Task<PipelineContext> Run(CancellationToken cancellationToken);
}
