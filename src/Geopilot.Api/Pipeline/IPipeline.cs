using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline;

internal interface IPipeline
{
    /// <summary>
    /// The unique id of the pipeline.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The pipelines display name. A human-readable name for the pipeline.
    /// </summary>
    Dictionary<string, string> DisplayName { get; set; }

    /// <summary>
    /// The parameters for the pipeline.
    /// </summary>
    PipelineParametersConfig Parameters { get; }

    /// <summary>
    /// The steps in the pipeline to be executed sequentially.
    /// </summary>
    List<PipelineStep> Steps { get; }

    /// <summary>
    /// The current state of the pipeline.
    /// </summary>
    PipelineState State { get; set; }

    /// <summary>
    /// Runs the pipeline with the specified input file.
    /// </summary>
    /// <param name="file">The input file to process.</param>
    /// <returns>The context containing the results of the pipeline execution.</returns>
    PipelineContext Run(FileHandle file);
}
