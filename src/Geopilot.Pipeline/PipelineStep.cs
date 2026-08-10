using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Reflection;

namespace Geopilot.Pipeline;

/// <summary>
/// Represents a single step in a pipeline.
/// </summary>
internal sealed class PipelineStep : IPipelineStep
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

    /// <summary>
    /// The compiled input values for this step, keyed by the target process parameter name.
    /// </summary>
    public IReadOnlyDictionary<string, InputValue> Inputs { get; }

    /// <inheritdoc/>
    public IReadOnlyList<OutputActionConfig> OutputActions { get; }

    /// <inheritdoc/>
    public PipelineStepConditionsConfig? StepConditions { get; }

    /// <inheritdoc/>
    public object Process { get; }

    /// <inheritdoc/>
    public StepState State { get; set; }

    /// <inheritdoc/>
    public LocalizedText? StatusMessage { get; private set; }

    /// <inheritdoc/>
    public LocalizedText? ConditionMessage { get; private set; }

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
    /// <param name="outputActions">The output actions for the step.</param>
    /// <param name="stepConditions">The step conditions for the step.</param>
    /// <param name="process">The process associated with the step.</param>
    /// <param name="pipelineDirectory">The pipeline working directory used to isolate input files copied into this step. When null (only when a step is constructed outside a job, for example in unit tests), input files are passed through without isolation.</param>
    /// <param name="resourcesDirectory">The resources directory that <c>${file(path)}</c> references resolve against. When null, a step that uses a file reference fails at run time.</param>
    /// <param name="logger">The logger to use for logging.</param>
    private PipelineStep(
        string id,
        LocalizedText displayName,
        IReadOnlyDictionary<string, InputValue> inputs,
        List<OutputActionConfig> outputActions,
        PipelineStepConditionsConfig? stepConditions,
        object process,
        string? pipelineDirectory,
        string? resourcesDirectory,
        ILogger logger)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this.Inputs = inputs;
        this.OutputActions = outputActions;
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
            var preOutcome = await EvaluatePreConditions(context);
            if (preOutcome is not null)
            {
                (this.State, this.ConditionMessage) = preOutcome.Value;
                return new StepResult();
            }

            this.State = StepState.Running;

            var stepResult = await ExecuteProcess(context, cancellationToken);
            var statusMessage = ExtractStatusMessage(stepResult);

            (this.State, this.ConditionMessage) = await EvaluatePostConditions(context, stepResult);

            this.StatusMessage = statusMessage;
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
        var resultTask = runMethod.Invoke(Process, runParams) as Task;

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

        PropertyInfo? prop = resultTask.GetType().GetProperty(nameof(Task<object>.Result));
        var result = prop?.GetValue(resultTask)
            ?? throw new PipelineRunException($"The process <{Process.GetType().Name}> did not return a result.");
        return new StepResult { Result = result };
    }

    private async Task<List<ConditionConfig>> FindMatchingConditions(List<ConditionConfig>? conditions, Dictionary<string, object?> parameters)
    {
        var matched = new List<ConditionConfig>();
        if (conditions != null)
        {
            foreach (var condition in conditions)
            {
                if (await this.conditionEvaluator.EvaluateConditionAsync(condition.Expression, parameters))
                    matched.Add(condition);
            }
        }

        return matched;
    }

    // PRE-conditions gate whether the step runs at all, evaluated in precedence (fail, then skip). A match
    // returns the gating state; null means no gate matched and the step proceeds to execution.
    private async Task<(StepState State, LocalizedText? ConditionMessage)?> EvaluatePreConditions(PipelineContext context)
    {
        var pre = this.StepConditions?.Pre;
        var parameters = context.ToExpressionParameters();

        var failConditions = await this.FindMatchingConditions(pre?.FailConditions, parameters);
        if (failConditions.Count > 0)
        {
            logger.LogInformation($"step failed due to pre-condition.");
            return (StepState.Error, MergeConditionMessages(failConditions));
        }

        var skipConditions = await this.FindMatchingConditions(pre?.SkipConditions, parameters);
        if (skipConditions.Count > 0)
        {
            logger.LogInformation($"step skipped due to pre-condition.");
            return (StepState.Skipped, MergeConditionMessages(skipConditions));
        }

        return null;
    }

    // POST-conditions run after the step. They are evaluated in a fixed precedence (fail, then
    // restrict-delivery, then warn); the first matching type determines the step's terminal state, and
    // if none match the step succeeds. Each type is a flat guard clause, so adding a type adds no nesting.
    private async Task<(StepState State, LocalizedText? ConditionMessage)> EvaluatePostConditions(PipelineContext context, StepResult stepResult)
    {
        var post = this.StepConditions?.Post;
        var parameters = context.ToExpressionParameters(this.Id, stepResult);

        var failConditions = await this.FindMatchingConditions(post?.FailConditions, parameters);
        if (failConditions.Count > 0)
        {
            logger.LogInformation($"failed due to post-condition.");
            return (StepState.Error, MergeConditionMessages(failConditions));
        }

        var restrictDeliveryConditions = await this.FindMatchingConditions(post?.RestrictDeliveryConditions, parameters);
        if (restrictDeliveryConditions.Count > 0)
        {
            logger.LogInformation($"delivery restricted due to post-condition.");
            return (StepState.DeliveryRestriction, MergeConditionMessages(restrictDeliveryConditions));
        }

        var warnConditions = await this.FindMatchingConditions(post?.WarnConditions, parameters);
        if (warnConditions.Count > 0)
        {
            logger.LogInformation($"completed with warnings due to post-condition.");
            return (StepState.Warning, MergeConditionMessages(warnConditions));
        }

        logger.LogInformation($"run successfull.");
        return (StepState.Success, null);
    }

    private LocalizedText? ExtractStatusMessage(StepResult stepResult) =>
        CombineStatusMessages(OutputActions
            .Where(outputAction => outputAction.Actions.Contains(OutputAction.StatusMessage))
            .Select(outputAction => NormalizeStatusMessage(stepResult.ExtractProperty(outputAction.Property))));

    /// <summary>
    /// Combines status messages from different sources into a single localized text, dropping absent
    /// (<see langword="null"/>) parts and joining the remainder per language with " - ". Returns
    /// <see langword="null"/> when nothing remains.
    /// </summary>
    private static LocalizedText? CombineStatusMessages(IEnumerable<LocalizedText?> messages)
    {
        var present = messages.Where(message => message is not null).Cast<LocalizedText>().ToList();
        if (present.Count == 0)
            return null;

        return LocalizedText.Merge(present, " - ");
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

    private static LocalizedText? MergeConditionMessages(List<ConditionConfig> conditions)
    {
        var messages = conditions.Where(c => c.Message is not null).Select(c => c.Message!).ToList();
        if (messages.Count == 0)
            return null;

        return LocalizedText.Merge(messages, ", ");
    }

    private MethodInfo GetProcessRunMethod()
    {
        var processRunMethods = ProcessReflection.GetRunMethods(Process.GetType());

        if (processRunMethods.Count > 1)
        {
            throw new PipelineRunException($"Multiple methods found with PipelineProcessRunAttribute on process <{Process.GetType().Name}>.");
        }
        else if (processRunMethods.Count == 0)
        {
            throw new PipelineRunException($"No method found with PipelineProcessRunAttribute on process <{Process.GetType().Name}>. There should be exactly one.");
        }
        else
        {
            return processRunMethods[0];
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
        if (context.StepResults.TryGetValue(stepId, out var stepResult))
        {
            var prop = stepResult.Result?.GetType().GetProperty(outputName);
            if (prop?.CanRead == true)
            {
                var data = prop.GetValue(stepResult.Result);
                value = this.WrapInput(data);
                return true;
            }
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

    /// <summary>
    /// Returns a new builder to create instances of a <see cref="PipelineStep"/>.
    /// </summary>
    internal static PipelineStepBuilder Builder()
    {
        return new PipelineStepBuilder();
    }

    /// <summary>
    /// Builder to create instances of a <see cref="PipelineStep"/>.
    /// </summary>
    internal class PipelineStepBuilder
    {
        private string? id;
        private LocalizedText? displayName;
        private IReadOnlyDictionary<string, InputValue>? inputs;
        private List<OutputActionConfig>? outputActions;
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
        /// Sets the output actions of the <see cref="PipelineStep"/> that will be created by this builder.
        /// </summary>
        public PipelineStepBuilder OutputActions(List<OutputActionConfig> outputActions)
        {
            this.outputActions = outputActions;
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
            if (outputActions == null)
                throw new InvalidOperationException("outputActions is required to build a PipelineStep.");
            if (process == null)
                throw new InvalidOperationException("process is required to build a PipelineStep.");
            if (logger == null)
                throw new InvalidOperationException("logger is required to build a PipelineStep.");

            return new PipelineStep(id, displayName, inputs, outputActions, stepConditions, process, pipelineDirectory, resourcesDirectory, logger);
        }
    }
}
