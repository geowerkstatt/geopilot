using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Interface for a pipeline process. Implementing classes define specific processing logic within a pipeline.
/// </summary>
public interface IPipelineProcess
{
    /// <summary>
    /// Runs the process with the given input data.
    /// </summary>
    /// <param name="inputData">The input data for the process.</param>
    /// <returns>The output data from the process.</returns>
    Task<ProcessData> Run(ProcessData inputData);
}
