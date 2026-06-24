using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Pipeline;
using Geopilot.PipelineCore.Pipeline.Process;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Reflection;

namespace Geopilot.Pipeline;

/// <summary>
/// Represents a single step in a pipeline.
/// </summary>
public sealed class PipelineStep : IPipelineStep
{
    private bool disposed;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
            return;

        if (Process is IDisposable disposableProcess)
            disposableProcess.Dispose();

        disposed = true;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public LocalizedText DisplayName { get; }

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

    /// <inheritdoc/>
    public LocalizedText? StatusMessage { get; private set; }

    private ImmutableList<PersistedFile> downloads = ImmutableList<PersistedFile>.Empty;
    private ImmutableList<PersistedFile> deliveryFiles = ImmutableList<PersistedFile>.Empty;

    /// <inheritdoc/>
    public IReadOnlyList<PersistedFile> Downloads => downloads;

    /// <inheritdoc/>
    public IReadOnlyList<PersistedFile> DeliveryFiles => deliveryFiles;

    /// <inheritdoc/>
    public void AddDownload(PersistedFile file) =>
        ImmutableInterlocked.Update(ref downloads, static (list, f) => list.Add(f), file);

    /// <inheritdoc/>
    public void AddDeliveryFile(PersistedFile file) =>
        ImmutableInterlocked.Update(ref deliveryFiles, static (list, f) => list.Add(f), file);

    private ImmutableList<StepVisualization> visualizations = ImmutableList<StepVisualization>.Empty;

    /// <inheritdoc/>
    public IReadOnlyList<StepVisualization> Visualizations => visualizations;

    /// <inheritdoc/>
    public void AddVisualization(StepVisualization visualization) =>
        ImmutableInterlocked.Update(ref visualizations, static (list, v) => list.Add(v), visualization);

    private readonly ConditionEvaluator conditionEvaluator;

    private readonly string? pipelineDirectory;

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
    /// <param name="pipelineDirectory">The pipeline working directory used to isolate input files copied into this step. When null (only when a step is constructed outside a job, for example in unit tests), input files are passed through without isolation.</param>
    /// <param name="logger">The logger to use for logging.</param>
    private PipelineStep(
        string id,
        LocalizedText displayName,
        List<InputConfig> inputConfig,
        List<OutputConfig> outputConfig,
        PipelineStepConditionsConfig? stepConditions,
        object process,
        string? pipelineDirectory,
        ILogger logger)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.InputConfig = inputConfig;
        this.OutputConfigs = outputConfig;
        this.StepConditions = stepConditions;
        this.Process = process;
        this.pipelineDirectory = pipelineDirectory;
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
                    var preFailResult = CreateConditionStepResult(this.Id + "_status_message_pre_fail_condition", failConditions);
                    this.StatusMessage = ExtractStatusMessage(preFailResult);
                    return preFailResult;
                }

                var skipConditions = await this.FindMatchingSkipConditions(this.StepConditions.Pre, context);
                if (skipConditions.Count > 0)
                {
                    this.State = StepState.Skipped;
                    logger.LogInformation($"step skipped due to pre-condition.");
                    var preSkipResult = CreateConditionStepResult(this.Id + "_status_message_pre_skip_condition", skipConditions);
                    this.StatusMessage = ExtractStatusMessage(preSkipResult);
                    return preSkipResult;
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

            this.StatusMessage = ExtractStatusMessage(stepResult);
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

    /// <summary>
    /// Merges all <see cref="OutputAction.StatusMessage"/> outputs from the step result into a
    /// single localized text. Multiple messages for the same language are joined with " - ".
    /// </summary>
    private static LocalizedText? ExtractStatusMessage(StepResult stepResult)
    {
        var messages = stepResult.Outputs
            .Where(o => o.Value.Action.Contains(OutputAction.StatusMessage))
            .Select(o => NormalizeStatusMessage(o.Value.Data))
            .Where(m => m is not null)
            .Cast<LocalizedText>()
            .ToList();

        if (messages.Count == 0)
            return null;

        return LocalizedText.Merge(messages, " - ");
    }

    /// <summary>
    /// Coerces a raw StatusMessage output value to a <see cref="LocalizedText"/>. Accepts a
    /// <see cref="LocalizedText"/> (returned as-is) or a string-to-string dictionary (for
    /// backward compatibility with plugins that predate <see cref="LocalizedText"/>), and
    /// returns <see langword="null"/> for any other or missing value.
    /// </summary>
    internal static LocalizedText? NormalizeStatusMessage(object? data) => data switch
    {
        LocalizedText localized => localized,
        IReadOnlyDictionary<string, string> dictionary => new LocalizedText(dictionary),

        // Defensive fallback: dictionaries that implement only IDictionary, not IReadOnlyDictionary.
        IDictionary<string, string> dictionary => new LocalizedText(new Dictionary<string, string>(dictionary)),
        _ => null,
    };

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

    private static LocalizedText MergeConditionMessages(List<ConditionConfig> conditions) =>
        LocalizedText.Merge(
            conditions.Where(c => c.Message is not null).Select(c => c.Message!),
            ", ");

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
                return this.WrapInput(context.Upload);
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
        else if (IsParameterNullable(parameterInfo) && mappedValues.Count == 0)
            return null;
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
                            mappedValues.AddRange(collection.Select(this.WrapInput));
                        else
                            mappedValues.Add(this.WrapInput(stepOutput.Data));
                    }
                }
            }
        }

        return mappedValues;
    }

    /// <summary>
    /// Wraps a value that is about to be injected into the process run method so that input files
    /// (single files and file lists) are isolated per step via <see cref="CopyOnWriteFile"/>.
    /// Non-file values are passed through unchanged.
    /// </summary>
    private object? WrapInput(object? value)
    {
        if (this.pipelineDirectory is null)
            return value;

        return value switch
        {
            IPipelineFileList list => new PipelineFileList(list.Files.Select(this.WrapFile).ToList()),
            IPipelineFile file => this.WrapFile(file),
            _ => value,
        };
    }

    private IPipelineFile WrapFile(IPipelineFile file)
    {
        var directory = this.pipelineDirectory;
        return directory is null ? file : new CopyOnWriteFile(file, directory, this.Id);
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
                var action = outputConfig.Action ?? new HashSet<OutputAction>();
                if (action.Contains(OutputAction.Visualization) && processDataPart is not IVisualization)
                {
                    var visualizationError = $"Output <{outputConfig.As}> of process <{Process.GetType().Name}> is tagged as a visualization, but its value is <{processDataPart?.GetType().Name ?? "null"}> and not a Visualization<T> envelope. Build it with VisualizationFactory.";
                    logger.LogError(visualizationError);
                    throw new PipelineRunException(visualizationError);
                }

                var stepOutput = new StepOutput
                {
                    Data = processDataPart,
                    Action = action,
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

    /// <summary>
    /// Returns a new builder to create instances of a <see cref="PipelineStep"/>.
    /// </summary>
    public static PipelineStepBuilder Builder()
    {
        return new PipelineStepBuilder();
    }

    /// <summary>
    /// Builder to create instances of a <see cref="PipelineStep"/>.
    /// </summary>
    public class PipelineStepBuilder
    {
        private string? id;
        private LocalizedText? displayName;
        private List<InputConfig>? inputConfig;
        private List<OutputConfig>? outputConfig;
        private PipelineStepConditionsConfig? stepConditions;
        private object? process;
        private string? pipelineDirectory;
        private ILogger? logger;

        /// <summary>
        /// Sets the id of the <see cref="PipelineStep"/> that will be created by this builder.
        /// </summary>
        public PipelineStepBuilder Id(string id)
        {
            this.id = id;
            return this;
        }

        /// <summary>
        /// Sets the display name of the <see cref="PipelineStep"/> that will be created by this builder.
        /// </summary>
        public PipelineStepBuilder DisplayName(LocalizedText displayName)
        {
            this.displayName = displayName;
            return this;
        }

        /// <summary>
        /// Sets the input config of the <see cref="PipelineStep"/> that will be created by this builder.
        /// </summary>
        public PipelineStepBuilder InputConfig(List<InputConfig> inputConfig)
        {
            this.inputConfig = inputConfig;
            return this;
        }

        /// <summary>
        /// Sets the output of the <see cref="PipelineStep"/> that will be created by this builder.
        /// </summary>
        public PipelineStepBuilder OutputConfig(List<OutputConfig> outputConfig)
        {
            this.outputConfig = outputConfig;
            return this;
        }

        /// <summary>
        /// Sets the step conditions of the <see cref="PipelineStep"/> that will be created by this builder.
        /// </summary>
        public PipelineStepBuilder StepConditions(PipelineStepConditionsConfig? stepConditions)
        {
            this.stepConditions = stepConditions;
            return this;
        }

        /// <summary>
        /// Sets the process to be run by the <see cref="PipelineStep"/> that will be created by this builder.
        /// </summary>
        /// <remarks>
        /// The object passed in as the process must be an instance of a pipeline process class.
        /// </remarks>
        public PipelineStepBuilder Process(object process)
        {
            this.process = process;
            return this;
        }

        /// <summary>
        /// Sets the pipeline working directory used to isolate input files copied into the <see cref="PipelineStep"/> that will be created by this builder.
        /// </summary>
        public PipelineStepBuilder PipelineDirectory(string pipelineDirectory)
        {
            this.pipelineDirectory = pipelineDirectory;
            return this;
        }

        /// <summary>
        /// Sets the logger to be used by the <see cref="PipelineStep"/> that will be created by this builder.
        /// </summary>
        public PipelineStepBuilder Logger(ILogger logger)
        {
            this.logger = logger;
            return this;
        }

        /// <summary>
        /// Creates a new instance of a <see cref="PipelineStep"/> according to the configuration of this builder.
        /// </summary>
        /// <exception cref="InvalidOperationException">If the builder is not properly configured to create a new instance of a <see cref="PipelineStep"/>.</exception>
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

            return new PipelineStep(id, displayName, inputConfig, outputConfig, stepConditions, process, pipelineDirectory, logger);
        }
    }
}
