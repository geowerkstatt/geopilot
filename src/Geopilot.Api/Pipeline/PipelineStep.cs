using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a single step in a pipeline.
/// </summary>
internal class PipelineStep : IPipelineStep
{
    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public Dictionary<string, string> DisplayName { get; }

    /// <inheritdoc/>
    public List<InputConfig> InputConfig { get; }

    /// <inheritdoc/>
    public List<OutputConfig> OutputConfigs { get; }

    /// <inheritdoc/>
    public IPipelineProcess Process { get; }

    /// <inheritdoc/>
    public StepState State { get; set; }

    private ILogger<PipelineStep> logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PipelineStep>();

    public PipelineStep(
        string id,
        Dictionary<string, string> displayName,
        List<InputConfig> inputConfig,
        List<OutputConfig> outputConfig,
        IPipelineProcess process)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.InputConfig = inputConfig;
        this.OutputConfigs = outputConfig;
        this.Process = process;

        this.State = StepState.Pending;
    }

    /// <inheritdoc/>
    public StepResult Run(PipelineContext context)
    {
        this.State = StepState.Running;

        var inputProcessData = CreateProcessData(context);
        var outputProcessData = Process.Run(inputProcessData);
        var stepResult = CreateStepResult(outputProcessData);

        this.State = StepState.Success;

        return stepResult;
    }

    private StepResult CreateStepResult(ProcessData outputProcessData)
    {
        var stepResult = new StepResult();
        foreach (var outputConfig in OutputConfigs)
        {
            if (outputConfig.Take != null && outputConfig.As != null && outputProcessData.Data.TryGetValue(outputConfig.Take, out var processDataPart))
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
                this.State = StepState.Failed;
                var errMsg = $"error in step '{this.Id}': output config is missing 'take' or 'as', or output data not found in process data. this error should not occure. please consolidate the pipeline validation logic.";
                logger.LogError(errMsg);
                throw new InvalidOperationException(errMsg);
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
                    var processDataPart = new ProcessDataPart(stepOutput.Data);
                    processData.Data[inputConfig.As] = processDataPart;
                }
                else
                {
                    this.State = StepState.Failed;
                    var errMsg = $"error in step '{this.Id}': step result is missing output data 'take', or output data could not be found in the step result. this error should not occure. please consolidate the pipeline validation locig.";
                    logger.LogError(errMsg);
                    throw new InvalidOperationException(errMsg);
                }
            }
            else
            {
                this.State = StepState.Failed;
                var errMsg = $"error in step '{this.Id}': pipeline context is missing input config 'from', or step result could not be found in the context. this error should not occure. please consolidate the pipeline validation locig.";
                logger.LogError(errMsg);
                throw new InvalidOperationException(errMsg);
            }
        }

        return processData;
    }
}
