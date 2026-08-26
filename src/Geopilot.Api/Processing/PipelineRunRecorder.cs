using Geopilot.Api.Authorization;
using Geopilot.Api.Models;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;

namespace Geopilot.Api.Processing;

/// <inheritdoc cref="IPipelineRunRecorder"/>
public class PipelineRunRecorder : IPipelineRunRecorder
{
    private static readonly string AppVersion =
        typeof(PipelineRunRecorder).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(PipelineRunRecorder).Assembly.GetName().Version?.ToString()
        ?? string.Empty;

    private readonly Context context;
    private readonly IPipelineFactory pipelineFactory;
    private readonly IUploadStorage uploadStorage;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<PipelineRunRecorder> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineRunRecorder"/> class.
    /// </summary>
    public PipelineRunRecorder(
        Context context,
        IPipelineFactory pipelineFactory,
        IUploadStorage uploadStorage,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PipelineRunRecorder> logger)
    {
        this.context = context;
        this.pipelineFactory = pipelineFactory;
        this.uploadStorage = uploadStorage;
        this.httpContextAccessor = httpContextAccessor;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task RecordJobStartedAsync(ProcessingJob job, Mandate mandate, User? user, UploadInfo upload)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(mandate);
        ArgumentNullException.ThrowIfNull(upload);

        var pipelineId = job.Pipeline?.Id
            ?? throw new InvalidOperationException($"Job <{job.Id}> has no pipeline attached; the run record needs its id.");

        var run = new PipelineRun
        {
            JobId = job.Id,
            PipelineId = pipelineId,
            Definition = pipelineFactory.GetDefinitionSnapshotJson(pipelineId),
            AppVersion = AppVersion,
            MandateId = mandate.Id,
            UserId = user?.Id,
            ClientKind = ClassifyClient(),
            UploadId = upload.Id,
            UploadStorageLocation = uploadStorage.StorageLocation,
            UploadInitiatedAt = upload.CreatedAt,
            StartedAt = DateTime.UtcNow,
            ScanState = ScanState.NotScanned,
            Files = upload.Files
                .Select(file => new PipelineRunFile
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

        context.PipelineRuns.Add(run);
        await context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task RecordScanOutcomeAsync(Guid jobId, ScanResult scanResult)
    {
        ArgumentNullException.ThrowIfNull(scanResult);

        try
        {
            var run = await context.PipelineRuns.Include(r => r.Files).SingleOrDefaultAsync(r => r.JobId == jobId);
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
            var run = await context.PipelineRuns.SingleOrDefaultAsync(r => r.JobId == jobId);
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
            var run = await context.PipelineRuns.SingleOrDefaultAsync(r => r.JobId == jobId);
            if (run is null)
            {
                LogRunMissing(jobId);
                return;
            }

            var row = await context.PipelineRunSteps
                .SingleOrDefaultAsync(s => s.PipelineRunId == run.Id && s.StepId == step.Id);
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
    public async Task RecordStepCompletedAsync(Guid jobId, IPipelineStep pipelineStep, int order)
    {
        ArgumentNullException.ThrowIfNull(pipelineStep);
        var step = pipelineStep;

        try
        {
            var run = await context.PipelineRuns.SingleOrDefaultAsync(r => r.JobId == jobId);
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
    public async Task RecordRunFinishedAsync(Guid jobId, IReadOnlyList<IPipelineStep> steps, ProcessingState terminalState, string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(steps);

        try
        {
            var run = await context.PipelineRuns.SingleOrDefaultAsync(r => r.JobId == jobId);
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
                var row = await LoadStepRowAsync(run.Id, step.Id);
                if (row is null)
                    row = CreateMissingStepRow(run.Id, jobId, step, order);

                UpdateStepRow(row, step);
            }

            run.TerminalState = terminalState;
            run.TerminalAt = DateTime.UtcNow;
            run.FailureReason = failureReason;
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            LogWriteFailed(jobId, ex);
        }
    }

    /// <summary>
    /// Creates and tracks a step row outside the start record. Step rows are pre-created with the start
    /// record, so having to create one late is an anomaly worth noticing (e.g. a definition change between
    /// start and run); creating it anyway keeps the protocol complete.
    /// </summary>
    private PipelineRunStep CreateMissingStepRow(int runId, Guid jobId, IPipelineStep step, int order)
    {
        logger.LogWarning("No step row exists for job <{JobId}>, step <{StepId}>; creating it late.", jobId, step.Id);
        var row = NewStepRow(runId, step, order);
        context.PipelineRunSteps.Add(row);
        return row;
    }

    private static PipelineRunStep NewStepRow(int runId, IPipelineStep step, int order)
    {
        var processType = step.Process.GetType();
        var assemblyName = processType.Assembly.GetName();

        return new PipelineRunStep
        {
            PipelineRunId = runId,
            Order = order,
            StepId = step.Id,
            DisplayName = step.DisplayName,
            ProcessImplementation = processType.FullName ?? processType.Name,
            ProcessAssemblyName = assemblyName.Name,
            ProcessAssemblyVersion = assemblyName.Version?.ToString(),
            State = step.State,
        };
    }

    private static void UpdateStepRow(PipelineRunStep row, IPipelineStep step)
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
            row.Conditions.AddRange(step.ConditionEvaluations.Select(evaluation => new PipelineRunCondition
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

    private static void AddMissingArtifacts(PipelineRunStep row, ArtifactKind kind, IEnumerable<(string OriginalFileName, string PersistedFileName, bool? FromUpload)> artifacts)
    {
        foreach (var artifact in artifacts)
        {
            if (row.Artifacts.Any(existing => existing.Kind == kind && string.Equals(existing.PersistedFileName, artifact.PersistedFileName, StringComparison.Ordinal)))
                continue;

            row.Artifacts.Add(new PipelineRunArtifact
            {
                Kind = kind,
                OriginalFileName = artifact.OriginalFileName,
                PersistedFileName = artifact.PersistedFileName,
                FromUpload = artifact.FromUpload,
            });
        }
    }

    private async Task<PipelineRunStep?> LoadStepRowAsync(int runId, string stepId)
    {
        return await context.PipelineRunSteps
            .Include(s => s.Conditions)
            .Include(s => s.Artifacts)
            .SingleOrDefaultAsync(s => s.PipelineRunId == runId && s.StepId == stepId);
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
        logger.LogWarning("No run record exists for job <{JobId}>; skipping the protocol write.", jobId);

    private void LogWriteFailed(Guid jobId, Exception exception) =>
        logger.LogWarning(exception, "Writing the execution protocol for job <{JobId}> failed; the job continues.", jobId);
}
