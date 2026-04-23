using Geopilot.Api.Pipeline.Config;
using Geopilot.PipelineCore.Pipeline;
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
        if (Process is IDisposable disposableProcess)
            disposableProcess.Dispose();
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

    private readonly ConditionEvaluator conditionEvaluator;

    private ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineStep"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for the step.</param>
    /// <param name="displayName">The display name for the step.</param>
    /// <param name="inputConfig">The input configuration for the step.</param>
    /// <param name="outputConfig">The output configuration for the step.</param>
    /// <param name="stepConditions">The step conditions for the step.</param>
    /// <param name="process">The process associated with the step.</param>
    /// <param name="logger">The logger to use for logging.</param>
    private PipelineStep(
        string id,
        Dictionary<string, string> displayName,
        List<InputConfig> inputConfig,
        List<OutputConfig> outputConfig,
        PipelineStepConditionsConfig? stepConditions,
        object process,
        ILogger logger)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.InputConfig = inputConfig;
        this.OutputConfigs = outputConfig;
        this.StepConditions = stepConditions;
        this.Process = process;
        this.logger = logger;
        this.conditionEvaluator = new ConditionEvaluator(logger);
        this.State = StepState.Pending;
    }

    /// <inheritdoc/>
    public async Task<StepResult> Run(PipelineContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation($"run step.");
        try
        {
            if (this.StepConditions != null)
            {
                var failConditions = await this.FindMatchingFailConditions(this.StepConditions.Pre, context);
                if (failConditions.Count > 0)
                {
                    this.State = StepState.Error;
                    logger.LogInformation($"step failed due to pre-condition.");
                    return CreateConditionStepResult(this.Id + "_status_message_pre_fail_condition", failConditions);
                }

                var skipConditions = await this.FindMatchingSkipConditions(this.StepConditions.Pre, context);
                if (skipConditions.Count > 0)
                {
                    this.State = StepState.Skipped;
                    logger.LogInformation($"step skipped due to pre-condition.");
                    return CreateConditionStepResult(this.Id + "_status_message_pre_skip_condition", skipConditions);
                }
            }

            this.State = StepState.Running;

            var stepResult = await ExecuteProcess(context, cancellationToken);

            if (this.StepConditions != null)
            {
                var postFailConditions = await this.FindMatchingFailConditions(this.StepConditions.Post, context, stepResult);
                if (postFailConditions.Count > 0)
                {
                    this.State = StepState.Error;
                    logger.LogInformation($"failed due to post-condition.");
                    AddConditionMessages(this.Id + "_status_message_post_fail_condition", stepResult, postFailConditions);
                }
                else
                {
                    this.State = StepState.Success;
                    logger.LogInformation($"run successfull.");
                }
            }
            else
            {
                this.State = StepState.Success;
                logger.LogInformation($"run successfull.");
            }

            return stepResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller (job timeout or host shutdown) cancelled us; that's not a
            // step failure. Mark the step Cancelled so the pipeline state getter and
            // downstream consumers can distinguish it from a crash.
            this.State = StepState.Cancelled;
            logger.LogInformation("Step cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            this.State = StepState.Error;
            logger.LogError(ex, $"Error in step.");
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Let cancellation propagate unwrapped so the caller (PipelineStep.Run ->
            // Pipeline.Run -> ValidationRunner) can recognize it and map to the
            // Cancelled state rather than treat it as a process failure.
            throw;
        }
        catch (Exception ex)
        {
            throw new PipelineRunException($"The process <{Process.GetType().Name}> threw an exception.", ex);
        }

        return CreateStepResult(resultTask.Result);
    }

    private async Task<List<ConditionConfig>> FindMatchingSkipConditions(PipelineStepPreConditionConfig? condition, PipelineContext context)
    {
        var matched = new List<ConditionConfig>();
        if (condition?.SkipConditions != null)
        {
            var expressionParameters = context.ToExpressionParameters();
            foreach (var skipCondition in condition.SkipConditions)
            {
                if (await this.conditionEvaluator.EvaluateConditionAsync(skipCondition.Expression, expressionParameters))
                    matched.Add(skipCondition);
            }
        }

        return matched;
    }

    private async Task<List<ConditionConfig>> FindMatchingFailConditions(PipelineStepPreConditionConfig? condition, PipelineContext context)
    {
        var matched = new List<ConditionConfig>();
        if (condition?.FailConditions != null)
        {
            var expressionParameters = context.ToExpressionParameters();
            foreach (var failCondition in condition.FailConditions)
            {
                if (await this.conditionEvaluator.EvaluateConditionAsync(failCondition.Expression, expressionParameters))
                    matched.Add(failCondition);
            }
        }

        return matched;
    }

    private async Task<List<ConditionConfig>> FindMatchingFailConditions(PipelineStepPostConditionConfig? condition, PipelineContext context, StepResult stepResult)
    {
        var matched = new List<ConditionConfig>();
        if (condition?.FailConditions != null)
        {
            var expressionParameters = context.ToExpressionParameters(this.Id, stepResult);
            foreach (var failCondition in condition.FailConditions)
            {
                if (await this.conditionEvaluator.EvaluateConditionAsync(failCondition.Expression, expressionParameters))
                    matched.Add(failCondition);
            }
        }

        return matched;
    }

    private static StepResult CreateConditionStepResult(string stepOutputKey, List<ConditionConfig> conditions)
    {
        var stepResult = new StepResult();
        AddConditionMessages(stepOutputKey, stepResult, conditions);
        return stepResult;
    }

    private static void AddConditionMessages(string stepOutputKey, StepResult stepResult, List<ConditionConfig> conditions)
    {
        var mergedMessages = MergeConditionMessages(conditions);
        if (mergedMessages.Count > 0)
        {
            stepResult.Outputs[stepOutputKey] = new StepOutput
            {
                Action = new HashSet<OutputAction> { OutputAction.StatusMessage },
                Data = mergedMessages,
            };
        }
    }

    private static Dictionary<string, string> MergeConditionMessages(List<ConditionConfig> conditions)
    {
        var allLanguages = conditions
            .Where(c => c.Message != null)
            .SelectMany(c => c.Message!.Keys)
            .Distinct();

        var merged = new Dictionary<string, string>();
        foreach (var language in allLanguages)
        {
            var messages = conditions
                .Where(c => c.Message != null && c.Message.ContainsKey(language))
                .Select(c => c.Message![language]);
            merged[language] = string.Join(", ", messages);
        }

        return merged;
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
            return cancellationToken;

        var uploadFilesAttribute = parameterInfo.GetCustomAttribute<UploadFilesAttribute>();
        if (uploadFilesAttribute != null)
        {
            if (parameterInfo.ParameterType.IsAssignableFrom(context.Upload.GetType()))
                return context.Upload;
            else if (IsArrayElementNullable(parameterInfo))
                return null;
            throw new PipelineRunException($"The parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType.FullName}> was marked with the UploadFilesAttribute, but was not assignable from the injected upload files collection of type <{context.Upload.GetType().FullName}>.");
        }

        // get all mapped values for the parameter based on the step's input config and the pipeline context
        var mappedValues = CollectMappedValues(parameterInfo, context);

        if (parameterInfo.ParameterType.IsArray)
            return GenerateArrayParameter(parameterInfo, mappedValues);

        if (mappedValues.Count == 1)
            return GenerateSingleParameter(parameterInfo, mappedValues[0]);
        else
            throw new PipelineRunException($"<{mappedValues.Count}> values found for parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType.FullName}> in process run method.");
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
                        if (stepOutput.Data is IEnumerable<object?> collection)
                            mappedValues.AddRange(collection);
                        else
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

        var mappedValuesArray = mappedValues
            .SelectMany(p => p is IEnumerable<object?> enumerable ? enumerable : new List<object?> { p })
            .ToArray();

        var hasAnyNonAssignableValues = mappedValuesArray.Any(p => p != null && !elementType.IsAssignableFrom(p.GetType()));

        if (hasAnyNonAssignableValues)
        {
            var errorMessage = $"At least one of the mapped input values was not assignable to the element type <{elementType.Name}> of parameter <{parameterInfo.Name}> of type <{parameterInfo.ParameterType.FullName}>.";
            throw new PipelineRunException(errorMessage);
        }

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

    internal static PipelineStepBuilder Builder()
    {
        return new PipelineStepBuilder();
    }

    internal class PipelineStepBuilder
    {
        private string? id;
        private Dictionary<string, string>? displayName;
        private List<InputConfig>? inputConfig;
        private List<OutputConfig>? outputConfig;
        private PipelineStepConditionsConfig? stepConditions;
        private object? process;
        private ILogger? logger;

        public PipelineStepBuilder Id(string id)
        {
            this.id = id;
            return this;
        }

        public PipelineStepBuilder DisplayName(Dictionary<string, string> displayName)
        {
            this.displayName = displayName;
            return this;
        }

        public PipelineStepBuilder InputConfig(List<InputConfig> inputConfig)
        {
            this.inputConfig = inputConfig;
            return this;
        }

        public PipelineStepBuilder OutputConfig(List<OutputConfig> outputConfig)
        {
            this.outputConfig = outputConfig;
            return this;
        }

        public PipelineStepBuilder StepConditions(PipelineStepConditionsConfig? stepConditions)
        {
            this.stepConditions = stepConditions;
            return this;
        }

        public PipelineStepBuilder Process(object process)
        {
            this.process = process;
            return this;
        }

        public PipelineStepBuilder Logger(ILogger logger)
        {
            this.logger = logger;
            return this;
        }

        public PipelineStep Build()
        {
            if (id == null)
                throw new InvalidOperationException("id is required to build a PipelineStep.");
            if (displayName == null)
                throw new InvalidOperationException("displayName is required to build a PipelineStep.");
            if (inputConfig == null)
                throw new InvalidOperationException("inputConfig is required to build a PipelineStep.");
            if (outputConfig == null)
                throw new InvalidOperationException("outputConfig is required to build a PipelineStep.");
            if (process == null)
                throw new InvalidOperationException("process is required to build a PipelineStep.");
            if (logger == null)
                throw new InvalidOperationException("logger is required to build a PipelineStep.");

            return new PipelineStep(id, displayName, inputConfig, outputConfig, stepConditions, process, logger);
        }
    }
}
