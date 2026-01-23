using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Interface for a pipeline process. Implementing classes define specific processing logic within a pipeline.
/// </summary>
internal interface IPipelineProcess
{
    /// <summary>
    /// The unique name of the process.
    /// </summary>
    string? Name { get; set; }

    /// <summary>
    /// The input and output data handling configuration for the process.
    /// </summary>
    DataHandlingConfig? DataHandlingConfig { get; set; }

    /// <summary>
    /// The defaul configuration settings for the process. Can be overridden in specific pipeline steps (<see cref="StepConfig.ProcessConfigOverwrites"/>).
    /// </summary>
    Dictionary<string, object>? DefaultConfig { get; set; }
}
