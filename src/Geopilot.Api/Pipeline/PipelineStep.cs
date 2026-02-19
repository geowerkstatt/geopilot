using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using System.Reflection;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Represents a single step in a pipeline.
/// </summary>
public sealed class PipelineStep : IPipelineStep
{
    /// <inheritdoc/>
    public void Dispose()
    {
        Process
            .GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => Attribute.IsDefined(m, typeof(PipelineProcessCleanupAttribute)))
            .ToList()
            .ForEach(m => m.Invoke(Process, null));
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public Dictionary<string, string> DisplayName { get; }

    /// <inheritdoc/>
    public List<InputConfig> InputConfig { get; }

    /// <inheritdoc/>
    public List<OutputConfig> OutputConfigs { get; }

    /// <inheritdoc/>
    public object Process { get; }

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
        object process)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.InputConfig = inputConfig;
        this.OutputConfigs = outputConfig;
        this.Process = process;

        this.State = StepState.Pending;
    }

    /// <inheritdoc/>
    public async Task<StepResult> Run(PipelineContext context, CancellationToken cancellationToken)
    {
        if (context != null)
        {
            this.State = StepState.Running;

            try
            {
                var runMethod = GetProcessRunMethod();

                if (runMethod != null)
                {
                    var runParams = CreateProcessRunParamList(context, runMethod.GetParameters().ToList(), cancellationToken).ToArray();
                    var resultTask = runMethod.Invoke(Process, runParams);
                    if (resultTask != null)
                    {
                        var result = await (Task<Dictionary<string, object>>)resultTask;
                        var stepResult = CreateStepResult(result);

                        this.State = StepState.Success;

                        return stepResult;
                    }
                    else
                    {
                        this.State = StepState.Failed;
                        logger.LogError($"error in step '{this.Id}': no result returned from process.");
                    }
                }
            }
            catch (Exception ex)
            {
                this.State = StepState.Failed;
                logger.LogError(ex, $"error in step '{this.Id}': exception occurred during step execution: {ex.Message}.");
            }
        }
        else
        {
            this.State = StepState.Failed;
            logger.LogError($"error in step '{this.Id}': pipeline context is null.");
        }

        return new StepResult();
    }

    private MethodInfo? GetProcessRunMethod()
    {
        var processRunMethods = Process.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => Attribute.IsDefined(m, typeof(PipelineProcessRunAttribute)))
                    .Where(m => m.ReturnType == typeof(Task<Dictionary<string, object>>));

        if (processRunMethods.Count() > 1)
        {
            logger.LogError($"error in step '{this.Id}': multiple methods found with PipelineProcessRunAttribute. there should only be one. please consolidate the pipeline validation logic.");
            return null;
        }
        else if (!processRunMethods.Any())
        {
            logger.LogError($"error in step '{this.Id}': no method found with PipelineProcessRunAttribute. there should be exactly one. please consolidate the pipeline validation logic.");
            return null;
        }
        else
        {
            return processRunMethods.First();
        }
    }

    private List<object> CreateProcessRunParamList(PipelineContext context, List<ParameterInfo> parameterInfos, CancellationToken cancellationToken)
    {
        return parameterInfos
            .Select(i => GenerateParameter(i, context, cancellationToken))
            .ToList();
    }

    private object GenerateParameter(ParameterInfo parameterInfo, PipelineContext context, CancellationToken cancellationToken)
    {
        if (parameterInfo.ParameterType.IsAssignableFrom(cancellationToken.GetType()))
        {
            return cancellationToken;
        }

        var mappedParameters = new List<object>();
        foreach (var inputConfig in this.InputConfig)
        {
            if (context.StepResults.TryGetValue(inputConfig.From, out var stepResult))
            {
                if (stepResult.Outputs.TryGetValue(inputConfig.Take, out var stepOutput))
                {
                    if (parameterInfo.Name == inputConfig.As)
                    {
                        mappedParameters.Add(stepOutput.Data);
                    }
                }
            }
        }

        if (parameterInfo.ParameterType.IsArray)
        {
            var elementType = parameterInfo.ParameterType.GetElementType() ?? throw new InvalidOperationException("could not get type of element");
            var parameterToInject = mappedParameters
                .Where(p => elementType.IsAssignableFrom(p.GetType()))
                .ToArray();
            var parameterOfCorrectTypeToInject = Array.CreateInstance(elementType, parameterToInject.Length);
            for (int i = 0; i < parameterToInject.Length; i++)
            {
                parameterOfCorrectTypeToInject.SetValue(parameterToInject[i], i);
            }

            if (parameterInfo.ParameterType.IsAssignableFrom(parameterOfCorrectTypeToInject.GetType()))
            {
                return parameterOfCorrectTypeToInject;
            }
        }

        if (mappedParameters.Count == 1 && parameterInfo.ParameterType.IsAssignableFrom(mappedParameters[0].GetType()))
        {
            return mappedParameters[0];
        }
        else
        {
            var errorMessage = $"error in step '{this.Id}': could not find matching data for parameter '{parameterInfo.Name}' of type '{parameterInfo.ParameterType.FullName}' in process run method. please consolidate the pipeline validation logic.";
            logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }
    }

    private StepResult CreateStepResult(Dictionary<string, object> outputProcessData)
    {
        var stepResult = new StepResult();
        foreach (var outputConfig in OutputConfigs)
        {
            if (outputConfig.Take != null && outputConfig.As != null && outputProcessData.TryGetValue(outputConfig.Take, out var processDataPart))
            {
                var stepOutput = new StepOutput
                {
                    Data = processDataPart,
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
}
