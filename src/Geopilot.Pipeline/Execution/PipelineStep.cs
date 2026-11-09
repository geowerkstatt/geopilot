using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Process;
using Geopilot.Pipeline.Visualization;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;

namespace Geopilot.Pipeline;

/// <summary>
/// Represents a single step in a pipeline.
/// </summary>
internal sealed class PipelineStep : IPipelineStep
{
    private const int MaxRenderedValueLength = 500;

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
    public DateTime? StartedAt { get; private set; }

    /// <inheritdoc/>
    public DateTime? FinishedAt { get; private set; }

    /// <inheritdoc/>
    public string? ErrorMessage { get; private set; }

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

    private ImmutableList<ConditionEvaluation> conditionEvaluations = ImmutableList<ConditionEvaluation>.Empty;

    /// <inheritdoc/>
    public IReadOnlyList<ConditionEvaluation> ConditionEvaluations => conditionEvaluations;

    private readonly ConditionEvaluator conditionEvaluator;

    private readonly string? pipelineDirectory;

    private readonly string? resourcesDirectory;

    private readonly IReadOnlyList<ConfiguredFile> configuredFiles;

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
    /// <param name="configuredFiles">The files the process configuration supplied to the constructor of the process.</param>
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
        IReadOnlyList<ConfiguredFile> configuredFiles,
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
        this.configuredFiles = configuredFiles;
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
        this.StartedAt = DateTime.UtcNow;
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

            this.StatusMessage = ExtractStatusMessage(stepResult);

            var postConditionsOutcome = await EvaluatePostConditions(context, stepResult);

            if (postConditionsOutcome.State != StepState.Error)
                ValidateOutputActionValues(stepResult);

            (this.State, this.ConditionMessage) = postConditionsOutcome;

            return stepResult;
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            // The caller (job timeout or host shutdown) cancelled us; that's not a
            // step failure. Mark the step Cancelled so the pipeline state getter and
            // downstream consumers can distinguish it from a crash.
            this.State = StepState.Cancelled;
            this.ErrorMessage = ex.Message;
            logger.LogInformation("Step cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            this.State = StepState.Error;
            this.ErrorMessage = ComposeErrorMessage(ex);
            logger.LogError(ex, $"Error in step.");
            throw;
        }
        finally
        {
            this.FinishedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Renders the exception that ended the step for <see cref="ErrorMessage"/>: the outer message names
    /// the failing part (e.g. the process), the innermost message carries the actual reason.
    /// </summary>
    private static string ComposeErrorMessage(Exception exception)
    {
        var baseException = exception.GetBaseException();
        return ReferenceEquals(baseException, exception)
            ? exception.Message
            : $"{exception.Message} {baseException.Message}";
    }

    private async Task<StepResult> ExecuteProcess(PipelineContext context, CancellationToken cancellationToken)
    {
        var runMethod = GetProcessRunMethod();
        var parameters = runMethod.GetParameters();
        var resolved = new Dictionary<InputValue, (ReferenceResolution Resolution, object? Value)>(ReferenceEqualityComparer.Instance);
        var runParams = parameters.Select(parameter => GenerateParameter(parameter, context, resolved, cancellationToken)).ToArray();

        if (context.OnStepInputsBound is { } onStepInputsBound)
            await onStepInputsBound(this, await DescribeInputsAsync(parameters, context, resolved, cancellationToken), cancellationToken);

        object? invocationResult;
        try
        {
            invocationResult = runMethod.Invoke(Process, runParams);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // A synchronously throwing run method surfaces as TargetInvocationException, whose own message
            // is reflection boilerplate. Unwrap it so the step's error message names the process and the
            // actual reason, consistent with the asynchronous failure path below.
            throw new PipelineRunException($"The process <{Process.GetType().Name}> threw an exception.", ex.InnerException);
        }

        var resultTask = invocationResult as Task;

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

    /// <summary>
    /// Evaluates the given conditions in order and returns the matching ones. Every evaluation, matching
    /// or not, is recorded on <see cref="ConditionEvaluations"/> together with the values the expression
    /// referenced, so a consumer can reconstruct why the step ended in its state.
    /// </summary>
    private async Task<List<ConditionConfig>> FindMatchingConditions(
        List<ConditionConfig>? conditions,
        ConditionPhase phase,
        ConditionKind kind,
        Dictionary<string, object?> parameters,
        IReadOnlySet<string> stepsWithoutResult)
    {
        var matched = new List<ConditionConfig>();
        if (conditions != null)
        {
            foreach (var condition in conditions)
            {
                var result = await this.conditionEvaluator.EvaluateConditionAsync(condition.Expression, parameters, stepsWithoutResult);

                var evaluation = new ConditionEvaluation(
                    condition.Id,
                    phase,
                    kind,
                    condition.Expression,
                    result.Matched,
                    RenderParameters(result.ReferencedParameters));
                ImmutableInterlocked.Update(ref conditionEvaluations, static (list, e) => list.Add(e), evaluation);

                if (result.Matched)
                    matched.Add(condition);
            }
        }

        return matched;
    }

    /// <summary>
    /// Renders the raw values a condition expression referenced into display strings for
    /// <see cref="ConditionEvaluation.Parameters"/>: scalars invariant-culture formatted and truncated,
    /// collections reduced to their count. Consumers need the evaluated values, not the objects.
    /// </summary>
    private static Dictionary<string, string?> RenderParameters(IReadOnlyDictionary<string, object?> parameters) =>
        parameters.ToDictionary(entry => entry.Key, entry => RenderValue(entry.Value));

    private static string? RenderValue(object? value) => value switch
    {
        null => null,
        string text => Truncate(text),
        LocalizedText localized => Truncate(string.Join(", ", localized.Languages.Select(language => $"{language}: {localized[language]}"))),
        Array array => $"<{array.Length} items>",
        System.Collections.ICollection collection => $"<{collection.Count} items>",

        // Also covers lazy sequences, which Convert.ToString would render as a CLR type name. Counting
        // enumerates them, which a step output has to tolerate anyway: conditions and inputs may read it
        // more than once.
        System.Collections.IEnumerable enumerable => $"<{enumerable.Cast<object>().Count()} items>",
        _ => Truncate(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    private static string? Truncate(string? text) =>
        text is { Length: > MaxRenderedValueLength } ? text[..MaxRenderedValueLength] + "..." : text;

    // PRE-conditions gate whether the step runs at all, evaluated in precedence (fail, then skip). A match
    // returns the gating state; null means no gate matched and the step proceeds to execution.
    private async Task<(StepState State, LocalizedText? ConditionMessage)?> EvaluatePreConditions(PipelineContext context)
    {
        var pre = this.StepConditions?.Pre;
        var parameters = context.ToExpressionParameters();
        var stepsWithoutResult = context.StepsWithoutResult();

        var failConditions = await this.FindMatchingConditions(pre?.FailConditions, ConditionPhase.Pre, ConditionKind.Fail, parameters, stepsWithoutResult);
        if (failConditions.Count > 0)
        {
            logger.LogInformation($"step failed due to pre-condition.");
            return (StepState.Error, MergeConditionMessages(failConditions));
        }

        var skipConditions = await this.FindMatchingConditions(pre?.SkipConditions, ConditionPhase.Pre, ConditionKind.Skip, parameters, stepsWithoutResult);
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
        var stepsWithoutResult = context.StepsWithoutResult();

        var failConditions = await this.FindMatchingConditions(post?.FailConditions, ConditionPhase.Post, ConditionKind.Fail, parameters, stepsWithoutResult);
        if (failConditions.Count > 0)
        {
            logger.LogInformation($"failed due to post-condition.");
            return (StepState.Error, MergeConditionMessages(failConditions));
        }

        var restrictDeliveryConditions = await this.FindMatchingConditions(post?.RestrictDeliveryConditions, ConditionPhase.Post, ConditionKind.RestrictDelivery, parameters, stepsWithoutResult);
        if (restrictDeliveryConditions.Count > 0)
        {
            logger.LogInformation($"delivery restricted due to post-condition.");
            return (StepState.DeliveryRestriction, MergeConditionMessages(restrictDeliveryConditions));
        }

        var warnConditions = await this.FindMatchingConditions(post?.WarnConditions, ConditionPhase.Post, ConditionKind.Warn, parameters, stepsWithoutResult);
        if (warnConditions.Count > 0)
        {
            logger.LogInformation($"completed with warnings due to post-condition.");
            return (StepState.Warning, MergeConditionMessages(warnConditions));
        }

        logger.LogInformation($"run successfull.");
        return (StepState.Success, null);
    }

    private void ValidateOutputActionValues(StepResult stepResult)
    {
        foreach (var outputAction in OutputActions)
        {
            var data = stepResult.ExtractProperty(outputAction.Property);

            if (outputAction.Actions.Contains(OutputAction.Download))
            {
                if (!(data is IPipelineFile || data is IEnumerable<IPipelineFile>))
                {
                    throw new PipelineRunException($"The output <{outputAction.Property}> is tagged as Download and must be an IPipelineFile or a collection of them, but is <{data?.GetType().Name ?? "null"}>.");
                }
            }

            if (outputAction.Actions.Contains(OutputAction.Visualization))
            {
                if (data is not IVisualization)
                {
                    throw new PipelineRunException($"The output <{outputAction.Property}> is tagged as Visualization and must be an IVisualization, but is <{data?.GetType().Name ?? "null"}>.");
                }
            }
        }
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

    /// <summary>
    /// Binds one run method parameter. Every reference the binding resolves is kept in <paramref name="resolved"/>,
    /// so the step can report what its process is given without resolving anything a second time.
    /// </summary>
    private object? GenerateParameter(ParameterInfo parameterInfo, PipelineContext context, Dictionary<InputValue, (ReferenceResolution Resolution, object? Value)> resolved, CancellationToken cancellationToken)
    {
        // The pipeline's cancellation token is injected directly, never bound from step input.
        if (parameterInfo.ParameterType == typeof(CancellationToken))
            return cancellationToken;

        var input = parameterInfo.Name != null ? Inputs.GetValueOrDefault(parameterInfo.Name) : null;

        var target = BindingTarget.FromParameter(parameterInfo);
        return InputBinder.Bind(
            target,
            input,
            (InputValue reference, out object? value) =>
            {
                var resolution = TryResolveReference(context, reference, out value);
                resolved[reference] = (resolution, value);
                return resolution;
            });
    }

    /// <summary>
    /// Describes what the process is given, for <see cref="PipelineContext.OnStepInputsBound"/>: per run method
    /// parameter the elements its sources yielded, in the order of the parameters and of the sources, followed by the
    /// files the process configuration supplied to the constructor.
    /// </summary>
    private async Task<IReadOnlyList<StepInput>> DescribeInputsAsync(ParameterInfo[] parameters, PipelineContext context, IReadOnlyDictionary<InputValue, (ReferenceResolution Resolution, object? Value)> resolved, CancellationToken cancellationToken)
    {
        var inputs = new List<StepInput>();
        foreach (var parameter in parameters)
        {
            if (parameter.Name is null || !Inputs.TryGetValue(parameter.Name, out var input))
                continue;

            var sources = input is InputValue.Sequence sequence ? sequence.Items : [input];
            foreach (var source in sources)
                inputs.AddRange(await DescribeSourceAsync(parameter.Name, source, context, resolved, cancellationToken));
        }

        foreach (var configuredFile in configuredFiles)
            inputs.Add(await DescribeFileAsync(configuredFile.Parameter, InputOrigin.Configuration, configuredFile.RelativePath, configuredFile.File, context, cancellationToken));

        return inputs;
    }

    /// <summary>
    /// Describes what one source of a parameter yielded: a literal its text, a reference each element of what it
    /// resolved to, spread one level like the binding spreads it. A reference to a skipped step yields nothing, which is
    /// recorded as such.
    /// </summary>
    private async Task<IReadOnlyList<StepInput>> DescribeSourceAsync(string parameter, InputValue source, PipelineContext context, IReadOnlyDictionary<InputValue, (ReferenceResolution Resolution, object? Value)> resolved, CancellationToken cancellationToken)
    {
        if (source is InputValue.Literal literal)
            return [new StepInput(parameter, InputOrigin.Definition, null, InputKind.Value, RenderValue(literal.Raw))];

        var (origin, reference) = source switch
        {
            InputValue.StepOutputReference stepOutput => (InputOrigin.StepOutput, $"{stepOutput.StepId}.{stepOutput.OutputName}"),
            InputValue.FileReference file => (InputOrigin.Resource, file.RelativePath),
            InputValue.UploadReference => (InputOrigin.Upload, (string?)null),
            _ => throw new UnreachableException($"Input value kind <{source.GetType().Name}> is no source."),
        };

        // The binding resolved every reference of the parameter before it got here.
        var (resolution, value) = resolved[source];
        if (resolution == ReferenceResolution.Absent)
            return [new StepInput(parameter, origin, reference, InputKind.Absent)];

        var elements = InputBinder.TryAsCollection(value, out var items) ? items : [value];
        var described = new List<StepInput>(elements.Count);
        foreach (var element in elements)
        {
            described.Add(element is IPipelineFile file
                ? await DescribeFileAsync(parameter, origin, reference, file, context, cancellationToken)
                : new StepInput(parameter, origin, reference, InputKind.Value, RenderValue(element)));
        }

        return described;
    }

    /// <summary>
    /// Describes a file the process is given. An uploaded file, also one an earlier step passed on unchanged, is left to
    /// the host, which knows its declared size and hash, so describing it never fetches it. A file on the local disk
    /// gets its size, and a resource or configured file also its hash, because those are files the deployment ships.
    /// </summary>
    private async Task<StepInput> DescribeFileAsync(string parameter, InputOrigin origin, string? reference, IPipelineFile file, PipelineContext context, CancellationToken cancellationToken)
    {
        var fileOrigin = file.UnwrapOrigin();
        if (context.Upload.Contains(fileOrigin, ReferenceEqualityComparer.Instance))
            return new StepInput(parameter, origin, reference, InputKind.File, FileName: file.OriginalFileName, FromUpload: true);

        var withHash = origin is InputOrigin.Resource or InputOrigin.Configuration;
        var (size, sha256) = fileOrigin is PipelineFile localFile
            ? await MeasureAsync(localFile, withHash, cancellationToken)
            : (null, null);

        return new StepInput(parameter, origin, reference, InputKind.File, FileName: file.OriginalFileName, Size: size, Sha256: sha256, FromUpload: false);
    }

    /// <summary>
    /// Reads the size and, when asked, the SHA-256 of a file on the local disk. A file that cannot be read is recorded
    /// without both: the protocol must not fail the step, and the process reports its own error when it reads the file.
    /// </summary>
    private async Task<(long? Size, string? Sha256)> MeasureAsync(PipelineFile file, bool withHash, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await file.OpenReadAsync(cancellationToken);
            var sha256 = withHash ? Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken)) : null;
            return (stream.Length, sha256);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read the input file <{FileName}>; it is recorded without size and hash.", file.OriginalFileName);
            return (null, null);
        }
    }

    /// <summary>
    /// Resolves an input reference to its runtime value: an earlier step's output or a
    /// <c>${file(...)}</c> resource. Input files are isolated per step via <see cref="CopyOnWriteFile"/>
    /// in the underlying resolvers.
    /// </summary>
    private ReferenceResolution TryResolveReference(PipelineContext context, InputValue reference, out object? value)
    {
        switch (reference)
        {
            case InputValue.StepOutputReference stepOutput:
                return TryResolveStepOutput(context, stepOutput.StepId, stepOutput.OutputName, out value);
            case InputValue.FileReference file:
                return TryResolveFileReference(file.RelativePath, out value)
                    ? ReferenceResolution.Resolved
                    : ReferenceResolution.Unresolvable;
            case InputValue.UploadReference:
                value = this.WrapInput(context.Upload);
                return ReferenceResolution.Resolved;
            default:
                value = null;
                return ReferenceResolution.Unresolvable;
        }
    }

    /// <summary>
    /// Resolves the value an earlier step published under <paramref name="outputName"/>, isolating
    /// any input files per step via <see cref="CopyOnWriteFile"/>. A step that ran no process, which
    /// is what a skipped step is, published nothing and therefore resolves as
    /// <see cref="ReferenceResolution.Absent"/> rather than as an error.
    /// </summary>
    private ReferenceResolution TryResolveStepOutput(PipelineContext context, string stepId, string outputName, out object? value)
    {
        value = null;

        if (!context.StepResults.TryGetValue(stepId, out var stepResult))
            return ReferenceResolution.Unresolvable;

        // A skipped step is recorded like any other, only without a process result. Its outputs are
        // absent, not unknown, so a later step binds them as "no value" instead of failing.
        if (stepResult.Result is null)
            return ReferenceResolution.Absent;

        var prop = stepResult.Result.GetType().GetProperty(outputName);
        if (prop?.CanRead != true)
            return ReferenceResolution.Unresolvable;

        value = this.WrapInput(prop.GetValue(stepResult.Result));
        return ReferenceResolution.Resolved;
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
        private IReadOnlyList<ConfiguredFile> configuredFiles = [];
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
        /// Sets the files the process configuration supplied to the constructor of the process, which the
        /// <see cref="PipelineStep"/> that will be created by this builder records with its inputs. None by default.
        /// </summary>
        public PipelineStepBuilder ConfiguredFiles(IReadOnlyList<ConfiguredFile> configuredFiles)
        {
            this.configuredFiles = configuredFiles;
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

            return new PipelineStep(id, displayName, inputs, outputActions, stepConditions, process, configuredFiles, pipelineDirectory, resourcesDirectory, logger);
        }
    }
}
