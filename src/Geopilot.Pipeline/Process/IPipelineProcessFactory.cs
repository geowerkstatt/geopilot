using Geopilot.Pipeline.Config;

namespace Geopilot.Pipeline.Process;

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

    /// <summary>
    /// Resolves each step's process result type once for a whole pipeline, keyed by step id. Build this a
    /// single time per pipeline and pass it to every step's validation via
    /// <see cref="IPipelineProcessBuilder.StepResultTypes"/>, so the map is not rebuilt per step. Steps
    /// whose process, type, or result type cannot be resolved are omitted.
    /// </summary>
    /// <param name="steps">All steps of the pipeline.</param>
    /// <param name="processes">The processes the steps reference.</param>
    /// <returns>A map from step id to the step's process result type.</returns>
    public IReadOnlyDictionary<string, Type> BuildStepResultTypes(List<StepConfig> steps, List<ProcessConfig> processes);
}
