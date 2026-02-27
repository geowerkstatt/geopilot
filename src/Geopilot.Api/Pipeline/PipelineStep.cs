using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Swashbuckle.AspNetCore.SwaggerGen;
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

        var errorMessage = $"Error in step <{this.Id}>: could not find matching data for parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType.FullName}> in process run method. please consolidate the pipeline validation logic.";
        logger.LogError(errorMessage);
        throw new InvalidOperationException(errorMessage);
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

    private Array? GenerateArrayParameter(ParameterInfo parameterInfo, List<object?> mappedValues)
    {
        var elementType = parameterInfo.ParameterType.GetElementType()
            ?? throw new InvalidOperationException("Could not get type of element.");

        var isElementNullable = IsArrayElementNullable(parameterInfo);
        var hasNullValues = mappedValues.Any(p => p == null);

        if (!isElementNullable && hasNullValues)
        {
            var errorMessage = $"Error in step <{this.Id}>: parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType.FullName}> is a non-nullable array, but at least one input was null.";
            logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        var hasAnyNonAssignableValues = mappedValues.Any(p => p != null && !elementType.IsAssignableFrom(p.GetType()));

        if (hasAnyNonAssignableValues)
        {
            var errorMessage = $"Error in step <{this.Id}>: at least one of the mapped input values was not assignable to the element type <{elementType.Name}> of parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType.FullName}>.";
            logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        var mappedValuesArray = mappedValues.ToArray();
        var arrayOfCorrectTypeToInject = Array.CreateInstance(elementType, mappedValuesArray.Length);
        for (int i = 0; i < mappedValuesArray.Length; i++)
        {
            arrayOfCorrectTypeToInject.SetValue(mappedValuesArray[i], i);
        }

        if (!parameterInfo.ParameterType.IsAssignableFrom(arrayOfCorrectTypeToInject.GetType()))
        {
            var errorMessage = $"Error in step <{this.Id}>: the generated array of type <{arrayOfCorrectTypeToInject.GetType()}> was not assignable to parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType}>.";
            logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        return arrayOfCorrectTypeToInject;
    }

    private object? GenerateSingleParameter(ParameterInfo parameterInfo, object? mappedValue)
    {
        if (mappedValue == null)
        {
            var isNullable = IsParameterNullable(parameterInfo);

            if (!isNullable)
            {
                var errorMessage = $"Error in step <{this.Id}>: the parameter <{parameterInfo.Name}> is non-nullable, but the mapped input value was null.";
                logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            return null;
        }

        if (!parameterInfo.ParameterType.IsAssignableFrom(mappedValue.GetType()))
        {
            var errorMessage = $"Error in step <{this.Id}>: the mapped input value of type <{mappedValue.GetType()}> was not assignable to parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType}>.";
            throw new InvalidOperationException(errorMessage);
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
                var errMsg = $"Error in step <{this.Id}>: output config is missing 'take' or 'as', or output data not found in process data. this error should not occur. please consolidate the pipeline validation logic.";
                logger.LogError(errMsg);
                throw new InvalidOperationException(errMsg);
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
