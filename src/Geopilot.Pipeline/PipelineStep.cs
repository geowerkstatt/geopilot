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
    public IReadOnlyDictionary<string, InputValue> Inputs { get; }

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

    private readonly string? resourcesDirectory;

    private ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineStep"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for the step.</param>
    /// <param name="displayName">The display name for the step.</param>
    /// <param name="inputs">The compiled input values for the step.</param>
    /// <param name="outputConfig">The output configuration for the step.</param>
    /// <param name="stepConditions">The step conditions for the step.</param>
    /// <param name="process">The process associated with the step.</param>
    /// <param name="pipelineDirectory">The pipeline working directory used to isolate input files copied into this step. When null (only when a step is constructed outside a job, for example in unit tests), input files are passed through without isolation.</param>
    /// <param name="resourcesDirectory">The resources directory that <c>${file(path)}</c> references resolve against. When null, a step that uses a file reference fails at run time.</param>
    /// <param name="logger">The logger to use for logging.</param>
    private PipelineStep(
        string id,
        LocalizedText displayName,
        IReadOnlyDictionary<string, InputValue> inputs,
        List<OutputConfig> outputConfig,
        PipelineStepConditionsConfig? stepConditions,
        object process,
        string? pipelineDirectory,
        string? resourcesDirectory,
        ILogger logger)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.Inputs = inputs;
        this.OutputConfigs = outputConfig;
        this.StepConditions = stepConditions;
        this.Process = process;
        this.pipelineDirectory = pipelineDirectory;
        this.resourcesDirectory = resourcesDirectory;
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
        // The pipeline's cancellation token is injected directly, never bound from step input.
        if (parameterInfo.ParameterType.IsAssignableFrom(cancellationToken.GetType()))
            return cancellationToken;

        var input = parameterInfo.Name != null ? Inputs.GetValueOrDefault(parameterInfo.Name) : null;

        var target = BindingTarget.FromParameter(parameterInfo);
        return InputBinder.Bind(
            target,
            input,
            (InputValue reference, out object? value) => TryResolveReference(context, reference, out value));
    }

    /// <summary>
    /// Resolves an input reference to its runtime value: an earlier step's output or a
    /// <c>${file(...)}</c> resource. Input files are isolated per step via <see cref="CopyOnWriteFile"/>
    /// in the underlying resolvers.
    /// </summary>
    private bool TryResolveReference(PipelineContext context, InputValue reference, out object? value)
    {
        switch (reference)
        {
            case InputValue.StepOutputReference stepOutput:
                return TryResolveStepOutput(context, stepOutput.StepId, stepOutput.OutputName, out value);
            case InputValue.FileReference file:
                return TryResolveFileReference(file.RelativePath, out value);
            case InputValue.UploadReference:
                value = this.WrapInput(context.Upload);
                return true;
            default:
                value = null;
                return false;
        }
    }

    /// <summary>
    /// Resolves the value an earlier step published under <paramref name="outputName"/>, isolating
    /// any input files per step via <see cref="CopyOnWriteFile"/>. Returns <see langword="false"/>
    /// when no such output exists.
    /// </summary>
    private bool TryResolveStepOutput(PipelineContext context, string stepId, string outputName, out object? value)
    {
        if (context.StepResults.TryGetValue(stepId, out var stepResult) &&
            stepResult.Outputs.TryGetValue(outputName, out var stepOutput))
        {
            value = this.WrapInput(stepOutput.Data);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Resolves a <c>${file(path)}</c> reference to the file under the resources directory, isolating
    /// it per step via <see cref="CopyOnWriteFile"/>. Throws when the resources directory is not
    /// configured or the file does not exist.
    /// </summary>
    private bool TryResolveFileReference(string relativePath, out object? value)
    {
        if (this.resourcesDirectory is null)
            throw new PipelineRunException($"Input references file '{relativePath}', but no resources directory is configured.");

        var fullPath = ResourceFileResolver.ResolveFullPath(this.resourcesDirectory, relativePath);
        if (!File.Exists(fullPath))
            throw new PipelineRunException($"Input references file '{relativePath}', which does not exist under the resources directory.");

        value = this.WrapInput(new PipelineFile(fullPath, Path.GetFileName(relativePath)));
        return true;
    }

    /// <summary>
    /// Wraps a value that is about to be injected into the process run method so that input files
    /// (single files, file lists and sequences of files) are isolated per step via
    /// <see cref="CopyOnWriteFile"/>. Non-file values are passed through unchanged.
    /// </summary>
    private object? WrapInput(object? value)
    {
        if (this.pipelineDirectory is null)
            return value;

        return value switch
        {
            IEnumerable<IPipelineFile> files => files.Select(this.WrapFile).ToArray(),
            IPipelineFile file => this.WrapFile(file),
            _ => value,
        };
    }

    private IPipelineFile WrapFile(IPipelineFile file)
    {
        var directory = this.pipelineDirectory;
        return directory is null ? file : new CopyOnWriteFile(file, directory, this.Id);
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
        private IReadOnlyDictionary<string, InputValue>? inputs;
        private List<OutputConfig>? outputConfig;
        private PipelineStepConditionsConfig? stepConditions;
        private object? process;
        private string? pipelineDirectory;
        private string? resourcesDirectory;
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
        /// Sets the compiled input values of the <see cref="PipelineStep"/> that will be created by this builder.
        /// </summary>
        public PipelineStepBuilder Inputs(IReadOnlyDictionary<string, InputValue> inputs)
        {
            this.inputs = inputs;
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
        /// Sets the resources directory that <c>${file(path)}</c> references resolve against for the <see cref="PipelineStep"/> that will be created by this builder.
        /// </summary>
        public PipelineStepBuilder ResourcesDirectory(string? resourcesDirectory)
        {
            this.resourcesDirectory = resourcesDirectory;
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
            if (inputs == null)
                throw new InvalidOperationException("inputs is required to build a PipelineStep.");
            if (outputConfig == null)
                throw new InvalidOperationException("outputConfig is required to build a PipelineStep.");
            if (process == null)
                throw new InvalidOperationException("process is required to build a PipelineStep.");
            if (logger == null)
                throw new InvalidOperationException("logger is required to build a PipelineStep.");

            return new PipelineStep(id, displayName, inputs, outputConfig, stepConditions, process, pipelineDirectory, resourcesDirectory, logger);
        }
    }
}
