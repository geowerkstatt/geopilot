using Geopilot.Api.Pipeline.Config;

namespace Geopilot.Api.Pipeline.Process;

/// <summary>
/// Defines a factory for creating pipeline process instances based on step and process configuration.
/// </summary>
/// <remarks>Implementations of this interface are responsible for constructing process objects that represent a
/// step in a pipeline, using the provided configuration data. The specific type of object returned by the factory may
/// vary depending on the implementation.</remarks>
public interface IPipelineProcessFactory
{
    /// <summary>
    /// Creates and initializes a new process based on the specified step configuration and a list of process
    /// configurations.
    /// </summary>
    /// <param name="stepConfig">The configuration settings for the step to be executed. Cannot be null.</param>
    /// <param name="processes">A list of process configurations that define the processes to be created. Cannot be null or empty.</param>
    /// <returns>An object representing the created process instance. The exact type and structure of the returned object depend
    /// on the implementation.</returns>
    public object CreateProcess(StepConfig stepConfig, List<ProcessConfig> processes);
}
