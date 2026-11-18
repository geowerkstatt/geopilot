using Geopilot.Api.Authorization;
using Geopilot.Api.Models;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Channels;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;

namespace Geopilot.Api.Processing;

/// <inheritdoc cref="IRecorder"/>
public class Recorder : IRecorder
{
    private static readonly string AppVersion = VersionOf(typeof(Recorder).Assembly) ?? string.Empty;

    private readonly Context context;
    private readonly IPipelineFactory pipelineFactory;
    private readonly IUploadStorage uploadStorage;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<Recorder> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Recorder"/> class.
    /// </summary>
    public Recorder(
        Context context,
        IPipelineFactory pipelineFactory,
        IUploadStorage uploadStorage,
        IHttpContextAccessor httpContextAccessor,
        ILogger<Recorder> logger)
    {
        this.context = context;
        this.pipelineFactory = pipelineFactory;
        this.uploadStorage = uploadStorage;
        this.httpContextAccessor = httpContextAccessor;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task RecordJobStartedAsync(ProcessingJob job, Mandate mandate, Supplier? supplier, UploadInfo upload)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(mandate);
        ArgumentNullException.ThrowIfNull(upload);

        var pipelineId = job.Pipeline?.Id
            ?? throw new InvalidOperationException($"Job <{job.Id}> has no pipeline attached; the recording needs its id.");

        var run = new Recording
        {
            JobId = job.Id,
            PipelineId = pipelineId,
            Definition = pipelineFactory.GetDefinitionSnapshotJson(pipelineId),
            AppVersion = AppVersion,
            MandateId = mandate.Id,
            UserId = supplier?.UserId,
            MachineClientId = supplier?.MachineClientId,
            ClientKind = ClassifyClient(),
            ClientReference = job.DeliveryIntent?.Fields.ClientReference,
            PermissionToDeliver = job.MayDeliver ? PermissionToDeliver.Permitted : PermissionToDeliver.NotPermitted,
            UploadId = upload.Id,
            UploadStorageLocation = uploadStorage.StorageLocation,
            UploadInitiatedAt = upload.CreatedAt,
            StartedAt = DateTime.UtcNow,
            ScanState = ScanState.NotScanned,
            Files = upload.Files
                .Select(file => new RecordingFile
                {
                    FileName = file.FileName,
                    StorageKey = file.StorageKey,
                    DeclaredSize = file.ExpectedSize,
                })
                .ToList(),

            // All step rows are created up front in Pending, without StartedAt, so the protocol shows
            // the full picture from the moment the job is accepted: a run interrupted during preflight
            // still lists what was planned, and a row left in Pending means the step was never reached.
            // The foreign key is set by EF through the navigation.
            Steps = job.Pipeline.Steps
                .Select((step, order) => NewStepRow(0, step, order))
                .ToList(),
        };

        context.Recordings.Add(run);
        await context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task RecordScanOutcomeAsync(Guid jobId, ScanResult scanResult)
    {
        ArgumentNullException.ThrowIfNull(scanResult);

        try
        {
            var run = await context.Recordings.Include(r => r.Files).SingleOrDefaultAsync(r => r.JobId == jobId);
            if (run is null)
            {
                LogRunMissing(jobId);
                return;
            }

            run.ScanState = !scanResult.Scanned
                ? ScanState.NotScanned
                : scanResult.IsClean ? ScanState.Clean : ScanState.ThreatDetected;
            run.ScanDetails = scanResult.ThreatDetails;

            if (scanResult.Hashes is { } hashes)
            {
                foreach (var file in run.Files)
                {
                    if (hashes.TryGetValue(file.StorageKey, out var hash))
                        file.Sha256 = hash;
                }
            }

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            LogWriteFailed(jobId, ex);
        }
    }

    /// <inheritdoc/>
    public async Task RecordPreflightFailedAsync(Guid jobId, string failureReason)
    {
        try
        {
            var run = await context.Recordings.SingleOrDefaultAsync(r => r.JobId == jobId);
            if (run is null)
            {
                LogRunMissing(jobId);
                return;
            }

            run.TerminalState = ProcessingState.Failed;
            run.TerminalAt = DateTime.UtcNow;
            run.FailureReason = failureReason;
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            LogWriteFailed(jobId, ex);
        }
    }

    /// <inheritdoc/>
    public async Task RecordStepStartedAsync(Guid jobId, IPipelineStep pipelineStep, int order)
    {
        ArgumentNullException.ThrowIfNull(pipelineStep);
        var step = pipelineStep;

        try
        {
            var run = await context.Recordings.SingleOrDefaultAsync(r => r.JobId == jobId);
            if (run is null)
            {
                LogRunMissing(jobId);
                return;
            }

            var row = await context.RecordingSteps
                .SingleOrDefaultAsync(s => s.RecordingId == run.Id && s.StepId == step.Id);
            if (row is null)
                row = CreateMissingStepRow(run.Id, jobId, step, order);

            row.State = StepState.Running;
            row.StartedAt = step.StartedAt ?? DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            LogWriteFailed(jobId, ex);
        }
    }

    /// <inheritdoc/>
    public async Task RecordStepInputsAsync(Guid jobId, IPipelineStep pipelineStep, int order, IReadOnlyList<StepInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(pipelineStep);
        ArgumentNullException.ThrowIfNull(inputs);

        try
        {
            var run = await context.Recordings.Include(r => r.Files).SingleOrDefaultAsync(r => r.JobId == jobId);
            if (run is null)
            {
                LogRunMissing(jobId);
                return;
            }

            var row = await context.RecordingSteps.SingleOrDefaultAsync(s => s.RecordingId == run.Id && s.StepId == pipelineStep.Id)
                ?? CreateMissingStepRow(run.Id, jobId, pipelineStep, order);

            row.Inputs.AddRange(inputs.Select(input => NewInputRow(input, run.Files)));
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            LogWriteFailed(jobId, ex);
        }
    }

    /// <inheritdoc/>
    public async Task RecordStepCompletedAsync(Guid jobId, IPipelineStep pipelineStep, int order)
    {
        ArgumentNullException.ThrowIfNull(pipelineStep);
        var step = pipelineStep;

        try
        {
            var run = await context.Recordings.SingleOrDefaultAsync(r => r.JobId == jobId);
            if (run is null)
            {
                LogRunMissing(jobId);
                return;
            }

            var row = await LoadStepRowAsync(run.Id, step.Id);
            if (row is null)
                row = CreateMissingStepRow(run.Id, jobId, step, order);

            UpdateStepRow(row, step);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            LogWriteFailed(jobId, ex);
        }
    }

    /// <inheritdoc/>
    public async Task RecordRunFinishedAsync(Guid jobId, IReadOnlyList<IPipelineStep> steps, ProcessingState terminalState, string? failureReason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);

        try
        {
            var run = await context.Recordings.SingleOrDefaultAsync(r => r.JobId == jobId, cancellationToken);
            if (run is null)
            {
                LogRunMissing(jobId);
                return;
            }

            // Reconcile every step: a step that threw got no completion callback, a step behind the
            // failure was never reached, and delivery artifacts are extracted only after the last step.
            for (var order = 0; order < steps.Count; order++)
            {
                var step = steps[order];
                var row = await LoadStepRowAsync(run.Id, step.Id, cancellationToken);
                if (row is null)
                    row = CreateMissingStepRow(run.Id, jobId, step, order);

                UpdateStepRow(row, step);
            }

            run.TerminalState = terminalState;
            run.TerminalAt = DateTime.UtcNow;
            run.FailureReason = failureReason;
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogWriteFailed(jobId, ex);
        }
    }

    /// <inheritdoc/>
    public async Task RecordDeliveryFailedAsync(Guid jobId, string reason)
    {
        try
        {
            // Bypasses the change tracker, which in a request may still hold a delivery whose save failed. The recording
            // tells whether the run allowed a delivery, which nobody checked for an expired job; a machine delivery is
            // created before the outcome is recorded, hence a missing terminal state (#1200).
            var updated = await context.Recordings
                .Where(r => r.JobId == jobId
                    && r.PermissionToDeliver == PermissionToDeliver.Permitted
                    && (r.TerminalState == null || r.TerminalState == ProcessingState.Success || r.TerminalState == ProcessingState.Warning))
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.DeliveryFailure, reason));

            if (updated == 0)
                logger.LogDebug("No recording of a run that allowed a delivery exists for job <{JobId}>; skipping the delivery failure.", jobId);
        }
        catch (Exception ex)
        {
            LogWriteFailed(jobId, ex);
        }
    }

    /// <inheritdoc/>
    public async Task RecordReactionAttemptStartedAsync(DeliveryReaction reaction, IReadOnlyDictionary<string, RecordedConfigValue>? config)
    {
        ArgumentNullException.ThrowIfNull(reaction);

        try
        {
            if (await FindRunOfDeliveryAsync(reaction.DeliveryId) is not int runId)
            {
                LogRunOfDeliveryMissing(reaction.DeliveryId);
                return;
            }

            context.RecordingReactionEvents.Add(NewAttemptRow(runId, reaction, config));
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            ForgetUnsavedReactionEvents();
            LogReactionWriteFailed(reaction.DeliveryId, ex);
        }
    }

    /// <inheritdoc/>
    public async Task RecordReactionAttemptFinishedAsync(DeliveryReaction reaction, IReadOnlyDictionary<string, RecordedConfigValue>? config, ReactionState? state, LocalizedText message)
    {
        ArgumentNullException.ThrowIfNull(reaction);

        try
        {
            if (await FindRunOfDeliveryAsync(reaction.DeliveryId) is not int runId)
            {
                LogRunOfDeliveryMissing(reaction.DeliveryId);
                return;
            }

            var row = await context.RecordingReactionEvents
                .Where(e => e.RecordingId == runId
                    && e.DeliveryId == reaction.DeliveryId
                    && e.ReactionId == reaction.ReactionId
                    && e.Event == ReactionEvent.Attempt
                    && e.Attempt == reaction.Attempts)
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();
            if (row is null)
            {
                logger.LogWarning("No row exists for attempt {Attempt} of the reaction <{ReactionId}> of delivery <{DeliveryId}>; creating it late.", reaction.Attempts, reaction.ReactionId, reaction.DeliveryId);
                row = NewAttemptRow(runId, reaction, config);
                context.RecordingReactionEvents.Add(row);
            }

            row.FinishedAt = DateTime.UtcNow;
            row.State = state;
            row.Message = message;
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            ForgetUnsavedReactionEvents();
            LogReactionWriteFailed(reaction.DeliveryId, ex);
        }
    }

    /// <inheritdoc/>
    public Task RecordDeliveryDeletedAsync(int deliveryId, IReadOnlyList<DeliveryReaction> reactions, int userId)
        => RecordWhatAUserDidAsync(deliveryId, reactions, ReactionEvent.Deletion, userId);

    /// <inheritdoc/>
    public Task RecordReactionsResetAsync(int deliveryId, IReadOnlyList<DeliveryReaction> reactions, int userId)
        => RecordWhatAUserDidAsync(deliveryId, reactions, ReactionEvent.Reset, userId);

    /// <summary>
    /// Writes one row per reaction for something a user did to them, with the state it left each reaction in.
    /// </summary>
    private async Task RecordWhatAUserDidAsync(int deliveryId, IReadOnlyList<DeliveryReaction> reactions, ReactionEvent reactionEvent, int userId)
    {
        ArgumentNullException.ThrowIfNull(reactions);
        if (reactions.Count == 0)
            return;

        try
        {
            if (await FindRunOfDeliveryAsync(deliveryId) is not int runId)
            {
                LogRunOfDeliveryMissing(deliveryId);
                return;
            }

            var occurredAt = DateTime.UtcNow;
            context.RecordingReactionEvents.AddRange(reactions.Select(reaction => new RecordingReactionEvent
            {
                RecordingId = runId,
                DeliveryId = deliveryId,
                ReactionId = reaction.ReactionId,
                ReactionKind = reaction.Kind,
                Implementation = reaction.Implementation,
                Event = reactionEvent,
                OccurredAt = occurredAt,
                State = reaction.State,
                Message = reaction.Message,
                UserId = userId,
            }));
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            ForgetUnsavedReactionEvents();
            LogReactionWriteFailed(deliveryId, ex);
        }
    }

    /// <summary>
    /// Finds the run a delivery comes from, also for a deleted delivery: the protocol outlives it.
    /// </summary>
    private Task<int?> FindRunOfDeliveryAsync(int deliveryId)
    {
        return context.Deliveries
            .IgnoreQueryFilters()
            .Where(delivery => delivery.Id == deliveryId)
            .Join(context.Recordings, delivery => delivery.JobId, run => run.JobId, (delivery, run) => (int?)run.Id)
            .SingleOrDefaultAsync();
    }

    private static RecordingReactionEvent NewAttemptRow(int runId, DeliveryReaction reaction, IReadOnlyDictionary<string, RecordedConfigValue>? config)
    {
        return new RecordingReactionEvent
        {
            RecordingId = runId,
            DeliveryId = reaction.DeliveryId,
            ReactionId = reaction.ReactionId,
            ReactionKind = reaction.Kind,
            Implementation = reaction.Implementation,
            Event = ReactionEvent.Attempt,
            Attempt = reaction.Attempts,
            OccurredAt = reaction.LastAttemptAt ?? DateTime.UtcNow,
            State = ReactionState.Running,
            AppVersion = AppVersion,
            Config = config is null ? null : new Dictionary<string, RecordedConfigValue>(config),
        };
    }

    /// <summary>
    /// Creates and tracks a step row outside the start record. Step rows are pre-created with the start
    /// record, so having to create one late is an anomaly worth noticing (e.g. a definition change between
    /// start and run); creating it anyway keeps the protocol complete.
    /// </summary>
    private RecordingStep CreateMissingStepRow(int runId, Guid jobId, IPipelineStep step, int order)
    {
        logger.LogWarning("No step row exists for job <{JobId}>, step <{StepId}>; creating it late.", jobId, step.Id);
        var row = NewStepRow(runId, step, order);
        context.RecordingSteps.Add(row);
        return row;
    }

    private static RecordingStep NewStepRow(int runId, IPipelineStep step, int order)
    {
        var processType = step.Process.GetType();
        var assemblyName = processType.Assembly.GetName();

        return new RecordingStep
        {
            RecordingId = runId,
            Order = order,
            StepId = step.Id,
            DisplayName = step.DisplayName,
            ProcessImplementation = processType.FullName ?? processType.Name,
            ProcessAssemblyName = assemblyName.Name,
            ProcessAssemblyVersion = VersionOf(processType.Assembly),
            State = step.State,
        };
    }

    // The informational version names the build: unlike the assembly version, it keeps a prerelease suffix such as 1.4.0-preview.12.
    private static string? VersionOf(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? assembly.GetName().Version?.ToString();

    /// <summary>
    /// Builds the row of one input. The runtime leaves an uploaded file to the manifest, so geopilot does not fetch it
    /// just to measure it: its size comes from there, and its hash too when the step took it from the upload itself. A
    /// file an earlier step passed on carries no hash, like every file of an earlier step.
    /// </summary>
    private static RecordingInput NewInputRow(StepInput input, IReadOnlyCollection<RecordingFile> manifest)
    {
        var uploadedFile = input.FromUpload == true
            ? manifest.FirstOrDefault(file => string.Equals(file.FileName, input.FileName, StringComparison.Ordinal))
            : null;

        return new RecordingInput
        {
            Parameter = input.Parameter,
            Origin = input.Origin,
            Reference = input.Reference,
            Kind = input.Kind,
            Value = input.Value,
            FileName = input.FileName,
            Size = uploadedFile?.DeclaredSize ?? input.Size,
            Sha256 = input.Origin == InputOrigin.Upload ? uploadedFile?.Sha256 : input.Sha256,
            FromUpload = input.FromUpload,
        };
    }

    private static void UpdateStepRow(RecordingStep row, IPipelineStep step)
    {
        row.State = step.State;
        row.StartedAt = step.StartedAt;
        row.FinishedAt = step.FinishedAt;
        row.ErrorMessage = step.ErrorMessage;
        row.StatusMessage = step.StatusMessage;
        row.ConditionMessage = step.ConditionMessage;

        // Evaluations are appended once per run of the step, so an existing set is simply complete.
        if (row.Conditions.Count == 0)
        {
            row.Conditions.AddRange(step.ConditionEvaluations.Select(evaluation => new RecordingCondition
            {
                ConditionId = evaluation.ConditionId,
                Phase = evaluation.Phase,
                Kind = evaluation.Kind,
                Expression = evaluation.Expression,
                Matched = evaluation.Matched,
                EvaluatedValues = evaluation.Parameters.Count > 0 ? JsonSerializer.Serialize(evaluation.Parameters) : null,
            }));
        }

        AddMissingArtifacts(row, ArtifactKind.Download, step.Downloads.Select(file => (file.OriginalFileName, file.PersistedFileName, (bool?)null)));
        AddMissingArtifacts(row, ArtifactKind.Visualization, step.Visualizations.Select(visualization => (visualization.OriginalFileName, visualization.PersistedFileName, (bool?)null)));
        AddMissingArtifacts(row, ArtifactKind.Delivery, step.DeliveryFiles.Select(file => (file.OriginalFileName, file.PersistedFileName, (bool?)file.FromUpload)));
    }

    private static void AddMissingArtifacts(RecordingStep row, ArtifactKind kind, IEnumerable<(string OriginalFileName, string PersistedFileName, bool? FromUpload)> artifacts)
    {
        foreach (var artifact in artifacts)
        {
            if (row.Artifacts.Any(existing => existing.Kind == kind && string.Equals(existing.PersistedFileName, artifact.PersistedFileName, StringComparison.Ordinal)))
                continue;

            row.Artifacts.Add(new RecordingArtifact
            {
                Kind = kind,
                OriginalFileName = artifact.OriginalFileName,
                PersistedFileName = artifact.PersistedFileName,
                FromUpload = artifact.FromUpload,
            });
        }
    }

    private async Task<RecordingStep?> LoadStepRowAsync(int runId, string stepId, CancellationToken cancellationToken = default)
    {
        return await context.RecordingSteps
            .Include(s => s.Conditions)
            .Include(s => s.Artifacts)
            .SingleOrDefaultAsync(s => s.RecordingId == runId && s.StepId == stepId, cancellationToken);
    }

    /// <summary>
    /// Classifies the caller from the current request, mirroring the token sources the authentication
    /// setup accepts: the geopilot.auth cookie marks the web frontend, a bearer header marks an API
    /// client. Without either (a job started anonymously on a public mandate), browser fetch metadata still
    /// identifies the web client. Only the classification is stored, never the raw header.
    /// </summary>
    private ClientKind ClassifyClient()
    {
        var request = httpContextAccessor.HttpContext?.Request;
        if (request is null)
            return ClientKind.Unknown;

        if (!string.IsNullOrEmpty(request.Cookies[AuthDefaults.AuthCookieName]))
            return ClientKind.WebClient;

        if (request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return ClientKind.ApiClient;

        if (request.Headers.ContainsKey("Sec-Fetch-Site") || request.Headers.ContainsKey("Origin"))
            return ClientKind.WebClient;

        return ClientKind.Unknown;
    }

    private void LogRunMissing(Guid jobId) =>
        logger.LogWarning("No recording exists for job <{JobId}>; skipping the protocol write.", jobId);

    private void LogWriteFailed(Guid jobId, Exception exception) =>
        logger.LogWarning(exception, "Writing the execution protocol for job <{JobId}> failed; the job continues.", jobId);

    private void LogRunOfDeliveryMissing(int deliveryId) =>
        logger.LogWarning("No recording exists for delivery <{DeliveryId}>; skipping the protocol write.", deliveryId);

    private void LogReactionWriteFailed(int deliveryId, Exception exception) =>
        logger.LogWarning(exception, "Writing the execution protocol for delivery <{DeliveryId}> failed.", deliveryId);

    // A row the database refused would otherwise go along with the next write of the same context and fail it again.
    private void ForgetUnsavedReactionEvents()
    {
        foreach (var entry in context.ChangeTracker.Entries<RecordingReactionEvent>().Where(entry => entry.State is EntityState.Added or EntityState.Modified).ToList())
            entry.State = EntityState.Detached;
    }
}
