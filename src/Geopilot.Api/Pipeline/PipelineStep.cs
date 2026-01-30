using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using System.Runtime.CompilerServices;

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
        this.OutputConfigs = outputConfig;
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
    public List<OutputConfig> OutputConfigs { get; }

    /// <summary>
    /// The process to be executed for this step.
    /// </summary>
    public IPipelineProcess Process { get; }

    public StepResult Run(PipelineContext context)
    {
        var inputProcessData = CreateProcessData(context);
        var outputProcessData = Process.Run(inputProcessData);
        var stepResult = CreateStepResult(outputProcessData);
        return stepResult;
    }

    private StepResult CreateStepResult(ProcessData outputProcessData)
    {
        var stepResult = new StepResult() { State = StepState.Success };
        foreach (var outputConfig in OutputConfigs)
        {
            if (outputProcessData.Data.TryGetValue(outputConfig.Take, out var processDataPart))
            {
                var stepOutput = new StepOutput
                {
                    Data = processDataPart.Data,
                    Action = outputConfig.Action ?? OutputAction.IGNORE,
                };
                stepResult.Outputs[outputConfig.As] = stepOutput;
            }
            else
            {
                // TODO: Handle missing output data
            }
        }

        return stepResult;
    }

    private ProcessData CreateProcessData(PipelineContext context)
    {
        var processData = new ProcessData();

        foreach (var inputConfig in this.InputConfig)
        {
            if (context.StepResults.TryGetValue(inputConfig.From, out var stepResult))
            {
                if (stepResult.Outputs.TryGetValue(inputConfig.Take, out var stepOutput))
                {
                    var processDataPart = new ProcessDataPart { Data = stepOutput.Data };
                    processData.Data[inputConfig.As] = processDataPart;
                }
                else
                {
                    // TODO: Handle missing output in step result
                }
            }
            else
            {
                // TODO: Handle missing step result in context
            }
        }

        return processData;
    }
}
