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
    /// Creates and returns a new pipeline process builder for configuring the steps and behavior of a pipeline process.
    /// </summary>
    /// <returns>An instance of IPipelineProcessBuilder that can be used to define and customize the pipeline process.</returns>
    public IPipelineProcessBuilder Builder();
}
