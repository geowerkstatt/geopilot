using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a single step in a pipeline.
/// </summary>
public class PipelineStep : IPipelineStep
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

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineStep"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for the step.</param>
    /// <param name="displayName">The display name for the step.</param>
    /// <param name="inputConfig">The input configuration for the step.</param>
    /// <param name="outputConfig">The output configuration for the step.</param>
    /// <param name="process">The process associated with the step.</param>
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
    public async Task<StepResult> Run(PipelineContext context)
    {
        if (context != null)
        {
            this.State = StepState.Running;

            try
            {
                var inputProcessData = CreateProcessData(context);
                var outputProcessData = await Process.Run(inputProcessData);
                var stepResult = CreateStepResult(outputProcessData);

                this.State = StepState.Success;

                return stepResult;
            }
            catch (Exception ex)
            {
                this.State = StepState.Failed;
                logger.LogError(ex, $"error in step '{this.Id}': exception occurred during step execution: {ex.Message}.");
                return new StepResult();
            }
        }
        else
        {
            this.State = StepState.Failed;
            return new StepResult();
        }
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
                    Action = outputConfig.Action ?? new HashSet<OutputAction>(),
                };
                stepResult.Outputs[outputConfig.As] = stepOutput;
            }
            else
            {
                var errMsg = $"error in step '{this.Id}': output config is missing 'take' or 'as', or output data not found in process data. this error should not occur. please consolidate the pipeline validation logic.";
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
                    var errMsg = $"error in step '{this.Id}': step result is missing output data 'take', or output data could not be found in the step result. this error should not occur. please consolidate the pipeline validation logic.";
                    logger.LogError(errMsg);
                    throw new InvalidOperationException(errMsg);
                }
            }
            else
            {
                var errMsg = $"error in step '{this.Id}': pipeline context is missing input config 'from', or step result could not be found in the context. this error should not occur. please consolidate the pipeline validation logic.";
                logger.LogError(errMsg);
                throw new InvalidOperationException(errMsg);
            }
        }

        return processData;
    }
}
