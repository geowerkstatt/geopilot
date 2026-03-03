using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline.Process;
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
    public PipelineStepConditionsConfig? StepConditions { get; }

    /// <inheritdoc/>
    public object Process { get; }

    /// <inheritdoc/>
    public StepState State { get; set; }

    private readonly ConditionEvaluator conditionEvaluator = new ConditionEvaluator();

    private ILogger<PipelineStep> logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PipelineStep>();

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineStep"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for the step.</param>
    /// <param name="displayName">The display name for the step.</param>
    /// <param name="inputConfig">The input configuration for the step.</param>
    /// <param name="outputConfig">The output configuration for the step.</param>
    /// <param name="stepConditions">The step conditions for the step.</param>
    /// <param name="process">The process associated with the step.</param>
    public PipelineStep(
        string id,
        Dictionary<string, string> displayName,
        List<InputConfig> inputConfig,
        List<OutputConfig> outputConfig,
        PipelineStepConditionsConfig? stepConditions,
        object process)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.InputConfig = inputConfig;
        this.OutputConfigs = outputConfig;
        this.StepConditions = stepConditions;
        this.Process = process;

        this.State = StepState.Pending;
    }

    /// <inheritdoc/>
    public async Task<StepResult> Run(PipelineContext context, CancellationToken cancellationToken)
    {
        try
        {
            if (this.StepConditions != null)
            {
                if (await this.FailStep(this.StepConditions.Pre, context))
                {
                    this.State = StepState.Error;
                    logger.LogInformation($"step '{this.Id}' failed due to pre-condition.");
                    return new StepResult();
                }
                else if (await this.SkipStepExecution(this.StepConditions.Pre, context))
                {
                    this.State = StepState.Skipped;
                    logger.LogInformation($"step '{this.Id}' skipped due to pre-condition.");
                    return new StepResult();
                }
            }

            this.State = StepState.Running;

            var stepResult = await ExecuteProcess(context, cancellationToken);

            if (this.StepConditions != null && await this.FailStep(this.StepConditions.Post, context, stepResult))
            {
                this.State = StepState.Error;
                logger.LogInformation($"step '{this.Id}' failed due to post-condition.");
            }
            else
            {
                this.State = StepState.Success;
            }

            return stepResult;
        }
        catch (Exception ex)
        {
            this.State = StepState.Error;
            logger.LogError(ex, $"Error in step <{this.Id}>.");
            throw;
        }
    }

    private async Task<StepResult> ExecuteProcess(PipelineContext context, CancellationToken cancellationToken)
    {
        var runMethod = GetProcessRunMethod();
        var runParams = CreateProcessRunParamList(context, runMethod.GetParameters().ToList(), cancellationToken).ToArray();
        var resultTask = runMethod.Invoke(Process, runParams) as Task<Dictionary<string, object>>;

        if (resultTask == null)
        {
            throw new PipelineRunException($"The process <{Process.GetType().Name}> did not return a task.");
        }

        try
        {
            await resultTask;
        }
        catch (Exception ex)
        {
            throw new PipelineRunException($"The process <{Process.GetType().Name}> threw an exception.", ex);
        }

        return CreateStepResult(resultTask.Result);
    }

    private async Task<bool> SkipStepExecution(PipelineStepPreConditionConfig? condition, PipelineContext context)
    {
        if (condition != null && condition.SkipCondition != null)
        {
            var expressionParameters = context.ToExpressionParameters();
            return await this.conditionEvaluator.EvaluateConditionAsync(condition.SkipCondition, expressionParameters);
        }
        else
        {
            return false;
        }
    }

    private async Task<bool> FailStep(PipelineStepPreConditionConfig? condition, PipelineContext context)
    {
        if (condition != null && condition.FailCondition != null)
        {
            var expressionParameters = context.ToExpressionParameters();
            return await this.conditionEvaluator.EvaluateConditionAsync(condition.FailCondition, expressionParameters);
        }
        else
        {
            return false;
        }
    }

    private async Task<bool> FailStep(PipelineStepPostConditionConfig? condition, PipelineContext context, StepResult stepResult)
    {
        if (condition != null && condition.FailCondition != null)
        {
            var expressionParameters = context.ToExpressionParameters(this.Id, stepResult);
            return await this.conditionEvaluator.EvaluateConditionAsync(condition.FailCondition, expressionParameters);
        }
        else
        {
            return false;
        }
    }

    private MethodInfo GetProcessRunMethod()
    {
        var processRunMethods = Process.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => Attribute.IsDefined(m, typeof(PipelineProcessRunAttribute)))
                    .Where(m => m.ReturnType == typeof(Task<Dictionary<string, object>>));

        if (processRunMethods.Count() > 1)
        {
            throw new PipelineRunException($"Multiple methods found with PipelineProcessRunAttribute on process <{Process.GetType().Name}>.");
        }
        else if (!processRunMethods.Any())
        {
            throw new PipelineRunException($"No method found with PipelineProcessRunAttribute on process <{Process.GetType().Name}>. There should be exactly one.");
        }
        else
        {
            return processRunMethods.First();
        }
    }

    private List<object?> CreateProcessRunParamList(PipelineContext context, List<ParameterInfo> parameterInfos, CancellationToken cancellationToken)
    {
        return parameterInfos
            .Select(i => GenerateParameter(i, context, cancellationToken))
            .ToList();
    }

    private object? GenerateParameter(ParameterInfo parameterInfo, PipelineContext context, CancellationToken cancellationToken)
    {
        // if the parameter is a cancellation token, inject the pipeline's cancellation token
        if (parameterInfo.ParameterType.IsAssignableFrom(cancellationToken.GetType()))
        {
            return cancellationToken;
        }

        // get all mapped values for the parameter based on the step's input config and the pipeline context
        var mappedValues = CollectMappedValues(parameterInfo, context);

        if (parameterInfo.ParameterType.IsArray)
        {
            return GenerateArrayParameter(parameterInfo, mappedValues);
        }

        if (mappedValues.Count == 1)
        {
            return GenerateSingleParameter(parameterInfo, mappedValues[0]);
        }

        var errorMessage = $"Could not find matching data for parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType.FullName}> in process run method.";
        throw new PipelineRunException(errorMessage);
    }

    private List<object?> CollectMappedValues(ParameterInfo parameterInfo, PipelineContext context)
    {
        var mappedValues = new List<object?>();
        foreach (var inputConfig in this.InputConfig)
        {
            if (context.StepResults.TryGetValue(inputConfig.From, out var stepResult))
            {
                if (stepResult.Outputs.TryGetValue(inputConfig.Take, out var stepOutput))
                {
                    if (parameterInfo.Name == inputConfig.As)
                    {
                        mappedValues.Add(stepOutput.Data);
                    }
                }
            }
        }

        return mappedValues;
    }

    private Array GenerateArrayParameter(ParameterInfo parameterInfo, List<object?> mappedValues)
    {
        var elementType = parameterInfo.ParameterType.GetElementType()
            ?? throw new InvalidOperationException("Could not get type of element.");

        var isElementNullable = IsArrayElementNullable(parameterInfo);
        var hasNullValues = mappedValues.Any(p => p == null);

        if (!isElementNullable && hasNullValues)
        {
            var errorMessage = $"Parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType.FullName}> is a non-nullable array, but at least one input was null.";
            throw new PipelineRunException(errorMessage);
        }

        var hasAnyNonAssignableValues = mappedValues.Any(p => p != null && !elementType.IsAssignableFrom(p.GetType()));

        if (hasAnyNonAssignableValues)
        {
            var errorMessage = $"At least one of the mapped input values was not assignable to the element type <{elementType.Name}> of parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType.FullName}>.";
            throw new PipelineRunException(errorMessage);
        }

        var mappedValuesArray = mappedValues.ToArray();
        var arrayOfCorrectTypeToInject = Array.CreateInstance(elementType, mappedValuesArray.Length);
        for (int i = 0; i < mappedValuesArray.Length; i++)
        {
            arrayOfCorrectTypeToInject.SetValue(mappedValuesArray[i], i);
        }

        if (!parameterInfo.ParameterType.IsAssignableFrom(arrayOfCorrectTypeToInject.GetType()))
        {
            var errorMessage = $"The generated array of type <{arrayOfCorrectTypeToInject.GetType()}> was not assignable to parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType}>.";
            throw new PipelineRunException(errorMessage);
        }

        return arrayOfCorrectTypeToInject;
    }

    private object? GenerateSingleParameter(ParameterInfo parameterInfo, object? mappedValue)
    {
        if (mappedValue == null && !IsParameterNullable(parameterInfo))
        {
            var errorMessage = $"The parameter <{parameterInfo.Name}> is non-nullable, but the mapped input value was null.";
            throw new PipelineRunException(errorMessage);
        }

        if (mappedValue != null && !parameterInfo.ParameterType.IsAssignableFrom(mappedValue.GetType()))
        {
            var errorMessage = $"The mapped input value of type <{mappedValue.GetType()}> was not assignable to parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType}>.";
            throw new PipelineRunException(errorMessage);
        }

        return mappedValue;
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
                var errorMessage = $"Output config is missing 'take' or 'as', or the process output (referenced by 'take') was not found in the output of the process. This error should not occur. Please consolidate the pipeline validation logic.";
                logger.LogError(errorMessage);
                throw new PipelineRunException(errorMessage);
            }
        }

        return stepResult;
    }

    private static bool IsParameterNullable(ParameterInfo parameterInfo)
    {
        return new NullabilityInfoContext().Create(parameterInfo).WriteState is NullabilityState.Nullable;
    }

    private static bool IsArrayElementNullable(ParameterInfo arrayParameterInfo)
    {
        var nullabilityInfo = new NullabilityInfoContext().Create(arrayParameterInfo);
        return nullabilityInfo.ElementType?.WriteState is NullabilityState.Nullable;
    }
}
