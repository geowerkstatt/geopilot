using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Defines a builder interface for configuring and constructing a pipeline process using a fluent API.
/// </summary>
/// <remarks>This interface enables sequential configuration of pipeline components, such as step settings,
/// process lists, pipeline directory, and job identification. Each configuration method returns the same builder
/// instance, allowing for method chaining. The final pipeline process is constructed by calling the Build method after
/// all desired configurations have been applied.</remarks>
public interface IPipelineProcessBuilder
{
    /// <summary>
    /// Configures the pipeline step with the specified settings.
    /// </summary>
    /// <remarks>Call this method before executing the pipeline to ensure the step is properly configured.
    /// Subsequent calls will overwrite previous configuration for the step.</remarks>
    /// <param name="stepConfig">The configuration settings to apply to the pipeline step. Cannot be null.</param>
    /// <returns>An instance of <see cref="IPipelineProcessBuilder"/> that can be used to further configure the pipeline.</returns>
    IPipelineProcessBuilder StepConfig(StepConfig stepConfig);

    /// <summary>
    /// Configures the pipeline with the specified collection of process configurations.
    /// </summary>
    /// <remarks>Use this method to dynamically specify the sequence and configuration of processes in the
    /// pipeline. Supplying an invalid or empty list will result in a runtime exception.</remarks>
    /// <param name="processes">A list of <see cref="ProcessConfig"/> objects that define the processes to include in the pipeline. The list
    /// must not be null or empty.</param>
    /// <returns>An <see cref="IPipelineProcessBuilder"/> instance that can be used to further configure the pipeline.</returns>
    IPipelineProcessBuilder Processes(List<ProcessConfig> processes);

    /// <summary>
    /// Configures the pipeline process to use the specified directory for locating pipeline files.
    /// </summary>
    /// <remarks>Ensure that the specified directory contains all required pipeline files for correct
    /// operation.</remarks>
    /// <param name="pipelineDirectory">The path to the directory that contains the pipeline files. The directory must exist and be accessible.</param>
    /// <returns>An instance of IPipelineProcessBuilder that can be used to further configure the pipeline process.</returns>
    IPipelineProcessBuilder PipelineDirectory(string pipelineDirectory);

    /// <summary>
    /// Sets the unique identifier for the job to be processed.
    /// </summary>
    /// <remarks>Ensure that the jobId provided is not an empty GUID, as this may lead to unexpected behavior
    /// during processing.</remarks>
    /// <param name="jobId">The unique identifier for the job. This value must be a valid GUID representing the job to be processed.</param>
    /// <returns>An instance of IPipelineProcessBuilder, allowing for further configuration of the pipeline process.</returns>
    IPipelineProcessBuilder JobId(Guid jobId);

    /// <summary>
    /// Builds and returns an object that represents the result of the pipeline construction process.
    /// </summary>
    /// <remarks>Call this method after configuring all necessary parameters and steps for the pipeline. The
    /// returned object may require casting or further processing, depending on the context in which it is
    /// used.</remarks>
    /// <returns>An object that encapsulates the configured pipeline or constructed result. The specific type and structure of
    /// the returned object depend on the implementation.</returns>
    object Build();
}
