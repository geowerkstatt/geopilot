using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Options;

namespace Geopilot.Api.Processing;

/// <summary>
/// The only writer of a processing job's state. Callers report what happened to a job (its preflight passed or
/// failed, its run finished, its retention ran out) and the lifecycle carries out every consequence: the state
/// change, the execution protocol, the disposal of the pipeline, the release of the upload and the machine
/// delivery. Which of them an outcome triggers is decided by <see cref="Decide"/>; what follows every event
/// happens without asking. Starting a job is not part of it yet.
/// </summary>
public class JobLifecycle
{
    private readonly ILogger<JobLifecycle> logger;
    private readonly IProcessingJobStore jobStore;
    private readonly ProcessingOptions processingOptions;
    private readonly IServiceScopeFactory serviceScopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobLifecycle"/> class.
    /// </summary>
    public JobLifecycle(
        ILogger<JobLifecycle> logger,
        IProcessingJobStore jobStore,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ProcessingOptions> processingOptions)
    {
        ArgumentNullException.ThrowIfNull(processingOptions);

        this.logger = logger;
        this.jobStore = jobStore;
        this.serviceScopeFactory = serviceScopeFactory;
        this.processingOptions = processingOptions.Value;
    }

    /// <summary>
    /// Reports that the pipeline of a job stopped running, however it ended, and carries out every consequence:
    /// the job's terminal state, the machine delivery, the execution protocol, the disposal of the pipeline and,
    /// as <see cref="Decide"/> says, the release of the upload. Apart from its argument checks it never throws:
    /// each consequence is guarded on its own, because the caller is the processing loop, which an escaping
    /// exception would stop.
    /// </summary>
    /// <param name="pipeline">The pipeline whose run ended. It is disposed here.</param>
    /// <param name="runOutcome">How the run ended.</param>
    /// <param name="cancellationToken">Signals a host shutdown: it cuts the machine delivery short and limits the
    /// protocol write to a short budget.</param>
    public async Task RunFinishedAsync(IPipeline pipeline, RunOutcome runOutcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(runOutcome);

        var jobId = pipeline.JobId;
        var decision = Decide(runOutcome);

        if (!jobStore.TryPipelineFinished(jobId, decision.State))
            logger.LogWarning("Job <{JobId}> did not successfully transition to <{State}>: it is unknown or no longer running.", jobId, decision.State);

        if (decision.State.IsDeliverable())
            await DeclareMachineDeliveryAsync(jobId, cancellationToken);

        // During a shutdown a slow database must not use up the time the host grants for stopping.
        // shortcut: fixed budget, derive it from the remaining shutdown time if 5 s turns out wrong.
        using var protocolCts = cancellationToken.IsCancellationRequested ? new CancellationTokenSource(TimeSpan.FromSeconds(5)) : new CancellationTokenSource();
        await RecordProtocolAsync(jobId, recorder => recorder.RecordRunFinishedAsync(jobId, pipeline.Steps, decision.State, DescribeFailure(runOutcome), protocolCts.Token));

        DisposePipeline(jobId, pipeline);

        if (decision.ReleaseUpload)
            await ReleaseUploadAsync(jobId);
    }

    /// <summary>
    /// Reports that the preflight of a pending job passed: records the scan outcome in the execution protocol and
    /// queues the job for its run, which turns it <see cref="ProcessingState.Running"/>.
    /// </summary>
    /// <param name="jobId">The job whose preflight passed.</param>
    /// <param name="scanResult">The outcome of the scan.</param>
    /// <param name="pipelineFiles">The uploaded files the run reads, registered but not fetched yet.</param>
    /// <exception cref="ArgumentException">If the job is unknown.</exception>
    /// <exception cref="InvalidOperationException">If the job is no longer pending or cannot be queued. The
    /// preflight reports such a job through <see cref="PreflightFailedAsync"/>, so it does not stay pending
    /// unnoticed.</exception>
    public async Task PreflightPassedAsync(Guid jobId, ScanResult scanResult, IReadOnlyList<IPipelineFile> pipelineFiles)
    {
        await RecordProtocolAsync(jobId, recorder => recorder.RecordScanOutcomeAsync(jobId, scanResult));
        jobStore.EnqueueForProcessing(jobId, pipelineFiles);
    }

    /// <summary>
    /// Reports that the preflight of a job failed and carries out what always follows: the failure, and the scan
    /// outcome if a scan ran, go into the execution protocol, the job turns <see cref="ProcessingState.Failed"/>,
    /// its pipeline is disposed and its upload released right away. Never throws, because the caller is the
    /// preflight loop.
    /// </summary>
    /// <param name="jobId">The job whose preflight failed.</param>
    /// <param name="failureReason">Why the preflight failed, as written to the execution protocol.</param>
    /// <param name="scanResult">The outcome of the scan if one ran, otherwise <see langword="null"/>.</param>
    public async Task PreflightFailedAsync(Guid jobId, string failureReason, ScanResult? scanResult)
    {
        await RecordProtocolAsync(jobId, async recorder =>
        {
            if (scanResult is not null)
                await recorder.RecordScanOutcomeAsync(jobId, scanResult);

            await recorder.RecordPreflightFailedAsync(jobId, failureReason);
        });

        if (!jobStore.TryMarkAsFailed(jobId))
            logger.LogWarning("Job <{JobId}> was not marked as failed: it is unknown or already in a terminal state.", jobId);

        // The pipeline was instantiated up front but never queued, so no run will dispose it.
        DisposePipeline(jobId, jobStore.GetJob(jobId)?.Pipeline);

        await ReleaseUploadAsync(jobId);
    }

    /// <summary>
    /// Declares the delivery of a machine delivery attempt on the caller's behalf. The handler is registered only
    /// while machine delivery is enabled, so a missing one means there is nothing to do.
    /// </summary>
    private Task DeclareMachineDeliveryAsync(Guid jobId, CancellationToken cancellationToken) =>
        CarryOutAsync(jobId, "declare the machine delivery", async services =>
        {
            if (services.GetService<IJobCompletionHandler>() is { } handler)
                await handler.OnJobFinishedAsync(jobId, cancellationToken);
        });

    /// <summary>
    /// Runs one protocol write. The recorder swallows its own write failures; the guard additionally covers
    /// resolving it.
    /// </summary>
    private Task RecordProtocolAsync(Guid jobId, Func<IPipelineRunRecorder, Task> record) =>
        CarryOutAsync(jobId, "write the execution protocol", services => record(services.GetRequiredService<IPipelineRunRecorder>()));

    /// <summary>
    /// Carries out one consequence against a fresh scope, because the services behind it depend on the scoped
    /// database context. Logs a failure instead of throwing, so it neither skips the consequences after it nor
    /// reaches the loop that reported the event.
    /// </summary>
    private async Task CarryOutAsync(Guid jobId, string consequence, Func<IServiceProvider, Task> carryOut)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            await carryOut(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not {Consequence} for job <{JobId}>.", consequence, jobId);
        }
    }

    /// <summary>
    /// Disposes the pipeline of a job that ended. It frees process-owned resources (e.g. HttpClient) right away.
    /// Pipeline state, step states, status-message dictionaries and PersistedDownloads survive disposal; what goes
    /// is the pipeline's temp directory, including the uploaded files fetched into it during the run. Guarded
    /// like every other consequence.
    /// </summary>
    private void DisposePipeline(Guid jobId, IPipeline? pipeline)
    {
        try
        {
            pipeline?.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to dispose the pipeline of job <{JobId}>.", jobId);
        }
    }

    /// <summary>
    /// The failure reason the execution protocol records for an outcome, or <see langword="null"/> when the
    /// pipeline ran to its end.
    /// </summary>
    private string? DescribeFailure(RunOutcome runOutcome) => runOutcome switch
    {
        RunOutcome.TimedOut => $"Job timed out after {processingOptions.JobTimeout}.",
        RunOutcome.ShutDown => "host shutdown",
        RunOutcome.Crashed crashed => crashed.Reason,
        RunOutcome.Completed { State: ProcessingState.Pending or ProcessingState.Running } completed => $"The pipeline returned in the non-terminal state <{completed.State}>.",
        _ => null,
    };

    /// <summary>
    /// Releases the uploaded files of the job. Only a known job carries the upload id. The release is a network
    /// call to the upload storage; the age-based sweep in UploadCleanupService picks up whatever it leaves behind.
    /// </summary>
    private async Task ReleaseUploadAsync(Guid jobId)
    {
        if (jobStore.GetJob(jobId) is not { } job)
            return;

        await CarryOutAsync(jobId, "release the upload", services =>
            services.GetRequiredService<IUploadOrchestrationService>().ReleaseUploadAsync(job.UploadId));
    }

    /// <summary>
    /// Decides what an outcome means for its job. A run that failed or was restricted by its own logic, a
    /// crashing process included, would end the same way a second time, so its upload goes right away. So does
    /// the upload of a deliverable run, whose delivery files are staged by now. A run that was
    /// <see cref="ProcessingState.Cancelled"/> (job timeout, host shutdown) says nothing about the data, so its
    /// upload stays for a second start until the age-based sweep in UploadCleanupService collects it. A failed
    /// preflight always releases its upload right away (<see cref="PreflightFailedAsync"/>), so it needs no
    /// decision.
    /// </summary>
    internal static JobDecision Decide(RunOutcome runOutcome) => runOutcome switch
    {
        RunOutcome.Completed(ProcessingState.Success) => new JobDecision(ProcessingState.Success, ReleaseUpload: true),

        RunOutcome.Completed(ProcessingState.Warning) => new JobDecision(ProcessingState.Warning, ReleaseUpload: true),

        RunOutcome.Completed(ProcessingState.DeliveryRestriction) => new JobDecision(ProcessingState.DeliveryRestriction, ReleaseUpload: true),

        RunOutcome.Completed(ProcessingState.Failed) => new JobDecision(ProcessingState.Failed, ReleaseUpload: true),

        RunOutcome.Completed(ProcessingState.Cancelled) or RunOutcome.TimedOut or RunOutcome.ShutDown => new JobDecision(ProcessingState.Cancelled, ReleaseUpload: false),

        // A pipeline that returns without a terminal state is a defect in our code, so it ends like a crash.
        RunOutcome.Crashed or RunOutcome.Completed(ProcessingState.Pending or ProcessingState.Running) => new JobDecision(ProcessingState.Failed, ReleaseUpload: true),

        _ => throw new ArgumentOutOfRangeException(nameof(runOutcome), runOutcome, "Unknown run outcome."),
    };
}

/// <summary>
/// The consequences a <see cref="RunOutcome"/> has for its job. <paramref name="State"/> is the state the job
/// moves to. <paramref name="ReleaseUpload"/> says whether the uploaded files are released right away; otherwise
/// they stay until the upload session expires, so a cancelled run can be started again.
/// </summary>
/// <remarks>
/// Only what differs between outcomes is listed. Disposing the pipeline and writing the execution protocol
/// happen for every outcome, so the job lifecycle carries them out without asking.
/// </remarks>
internal sealed record JobDecision(ProcessingState State, bool ReleaseUpload);
