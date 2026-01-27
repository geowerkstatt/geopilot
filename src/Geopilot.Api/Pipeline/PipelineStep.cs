using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using YamlDotNet.Serialization;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a single step in a pipeline.
/// </summary>
internal class PipelineStep
{
    public PipelineStep(string name, List<InputConfig> inputConfig, List<OutputConfig> outputConfig, IPipelineProcess process)
    {
        this.Name = name;
        this.InputConfig = inputConfig;
        this.OutputConfig = outputConfig;
        this.Process = process;
    }

    /// <summary>
    /// The name of the step. This name is unique within the pipeline. Other Steps reference this name to define data flow.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The input configuration for this step.
    /// </summary>
    public List<InputConfig> InputConfig { get; }

    /// <summary>
    /// The output configuration for this step.
    /// </summary>
    public List<OutputConfig> OutputConfig { get; }

    /// <summary>
    /// The process to be executed for this step.
    /// </summary>
    public IPipelineProcess Process { get; }
}
