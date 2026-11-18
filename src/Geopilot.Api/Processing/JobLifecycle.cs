using Geopilot.Api.Exceptions;
using Geopilot.Api.Models;
using Geopilot.Api.Services;
using Geopilot.Pipeline;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Geopilot.Api.Processing;

/// <summary>
/// The only writer of a processing job's state. Callers report what happened to a job (it was started, its
/// preflight passed or failed, its run finished, its retention ran out) and the lifecycle carries out every
/// consequence: the state change, the execution protocol, the disposal of the pipeline, the release of the upload
/// and the machine delivery. Which of them an outcome triggers is decided by <see cref="Decide"/>; what follows
/// every event happens without asking.
/// </summary>
public class JobLifecycle
{
    private readonly ILogger<JobLifecycle> logger;
    private readonly IProcessingJobStore jobStore;
    private readonly IPipelineFactory pipelineFactory;
    private readonly ChannelWriter<PreflightRequest> preflightQueue;
    private readonly ProcessingOptions processingOptions;
    private readonly RefMappingOptions refMappingOptions;
    private readonly IServiceScopeFactory serviceScopeFactory;

    private sealed record RefMappings(IReadOnlyList<RefMappingEntry> Entries) : IRefMappings;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobLifecycle"/> class.
    /// </summary>
    public JobLifecycle(
        ILogger<JobLifecycle> logger,
        IProcessingJobStore jobStore,
        IPipelineFactory pipelineFactory,
        ChannelWriter<PreflightRequest> preflightQueue,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ProcessingOptions> processingOptions,
        IOptions<RefMappingOptions> refMappingOptions)
    {
        ArgumentNullException.ThrowIfNull(processingOptions);
        ArgumentNullException.ThrowIfNull(refMappingOptions);

        this.logger = logger;
        this.jobStore = jobStore;
        this.pipelineFactory = pipelineFactory;
        this.preflightQueue = preflightQueue;
        this.serviceScopeFactory = serviceScopeFactory;
        this.processingOptions = processingOptions.Value;
        this.refMappingOptions = refMappingOptions.Value;
    }

    /// <summary>
    /// Starts a job for an upload against a mandate the caller has checked: creates the job, instantiates its
    /// pipeline with what the host binds to the job (the reference data mapping, where the installation maintains
    /// it), attaches it, records the start in the execution protocol and hands the job to the preflight. A
    /// start that fails once the job exists leaves no job behind: it is removed again and its pipeline disposed,
    /// while the upload stays for another start.
    /// </summary>
    /// <param name="upload">The upload whose files the job processes.</param>
    /// <param name="mandate">The mandate the upload is processed against.</param>
    /// <param name="pipelineId">The pipeline of the mandate, which the caller has checked to be set.</param>
    /// <param name="supplier">Who starts the job, or <see langword="null"/> for an anonymous start.</param>
    /// <param name="mayDeliver">Whether whoever started the run may deliver to its mandate, see <see cref="ProcessingJob.MayDeliver"/>.</param>
    /// <param name="deliveryFields">The delivery fields of a machine delivery, which geopilot then creates itself
    /// once the run allows it (see <see cref="ProcessingJob.DeliveryIntent"/>); <see langword="null"/> for a start
    /// whose delivery is created by hand, if at all.</param>
    /// <returns>The pending job with its pipeline attached.</returns>
    /// <exception cref="ArgumentException">If <paramref name="deliveryFields"/> come without a
    /// <paramref name="supplier"/> to create the delivery for.</exception>
    /// <exception cref="JobAlreadyActiveException">If a job for the upload is still pending or running. Nothing
    /// was created then.</exception>
    /// <remarks>
    /// Whatever creating the pipeline or writing the start record throws is rethrown: accepting a job is a promise,
    /// and without the record nothing could account for it (see <see cref="IRecorder"/>).
    /// </remarks>
    public async Task<ProcessingJob> StartAsync(UploadInfo upload, Mandate mandate, string pipelineId, Supplier? supplier, bool mayDeliver, DeliveryFields? deliveryFields)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(mandate);

        // Bound to the job when it is created, so its run cannot end before the intent is known. The key is the one
        // the mandate carries, not the one a request spelled, so every route echoes the same value.
        var deliveryIntent = deliveryFields is null
            ? null
            : new DeliveryIntent(
                supplier ?? throw new ArgumentException("A delivery can only be created for a known supplier.", nameof(supplier)),
                mandate.Key ?? string.Empty,
                deliveryFields);

        var job = jobStore.CreateJob(upload.Id, mayDeliver, deliveryIntent);
        IPipeline? pipeline = null;
        ProcessingJob jobWithPipeline;
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var bindings = await ReadJobBindingsAsync(scope.ServiceProvider);

            // Instantiated up front (without files), so the status response can render the pipeline's steps
            // while preflight runs. The pipeline is started once preflight passes.
            pipeline = pipelineFactory.CreatePipeline(pipelineId, job.Id, bindings);
            jobWithPipeline = jobStore.AttachPipeline(job.Id, pipeline, mandate.Id);

            // Not through CarryOutAsync, which would swallow the failure: the record must exist before anything
            // can crash the job.
            await scope.ServiceProvider.GetRequiredService<IRecorder>()
                .RecordJobStartedAsync(jobWithPipeline, mandate, supplier, upload, bindings.ToSnapshotJson());
        }
        catch
        {
            jobStore.RemoveJob(job.Id);
            DisposePipeline(job.Id, pipeline);
            throw;
        }

        await preflightQueue.WriteAsync(new PreflightRequest(job.Id, upload.Id));
        return jobWithPipeline;
    }

    /// <summary>
    /// Reads what the host binds to a job that starts now. The job keeps what was read, so an entry an administrator
    /// changes later does not reach a job that already started.
    /// </summary>
    internal async Task<PipelineJobBindings> ReadJobBindingsAsync(IServiceProvider services)
    {
        if (!refMappingOptions.Enabled)
            return PipelineJobBindings.None;

        // Not disposed here: the scope owns the context, and the recorder resolves the same instance afterwards.
        var context = services.GetRequiredService<Context>();
        var entries = await context.RefMappings
            .AsNoTracking()
            .OrderBy(entry => entry.Id)
            .Select(entry => new RefMappingEntry(entry.Scope, entry.RefDataUris, entry.Comment))
            .ToListAsync();
        return new PipelineJobBindings(new RefMappings(entries));
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
        var job = jobStore.GetJob(jobId);
        var decision = Decide(runOutcome);

        if (!jobStore.TryPipelineFinished(jobId, decision.State))
            logger.LogWarning("Job <{JobId}> did not successfully transition to <{State}>: it is unknown or no longer running.", jobId, decision.State);

        if (decision.State.IsDeliverable() && job?.DeliveryIntent is { } deliveryIntent)
            await CreateDeliveryAsync(jobId, deliveryIntent, cancellationToken);

        // During a shutdown a slow database must not use up the time the host grants for stopping.
        // shortcut: fixed budget, derive it from the remaining shutdown time if 5 s turns out wrong.
        using var protocolCts = cancellationToken.IsCancellationRequested ? new CancellationTokenSource(TimeSpan.FromSeconds(5)) : new CancellationTokenSource();
        await RecordProtocolAsync(jobId, recorder => recorder.RecordRunFinishedAsync(jobId, pipeline.Steps, decision.State, DescribeFailure(runOutcome), protocolCts.Token));

        DisposePipeline(jobId, pipeline);

        if (decision.ReleaseUpload)
            await ReleaseUploadAsync(jobId, job?.UploadId);
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
        var job = jobStore.GetJob(jobId);

        await RecordProtocolAsync(jobId, async recorder =>
        {
            if (scanResult is not null)
                await recorder.RecordScanOutcomeAsync(jobId, scanResult);

            await recorder.RecordPreflightFailedAsync(jobId, failureReason);
        });

        if (!jobStore.TryMarkAsFailed(jobId))
            logger.LogWarning("Job <{JobId}> was not marked as failed: it is unknown or already in a terminal state.", jobId);

        // The pipeline was instantiated up front but never queued, so no run will dispose it.
        DisposePipeline(jobId, job?.Pipeline);

        await ReleaseUploadAsync(jobId, job?.UploadId);
    }

    /// <summary>
    /// Retires a job whose retention has run out by removing it from the store. Neither its pipeline nor its
    /// upload is touched: the pipeline was disposed when the job ended (the cleanup never retires a job that is
    /// still pending or running), and the upload was released then too, or is kept on purpose until its upload
    /// session expires (a cancelled run, see <see cref="Decide"/>). An unknown job is fine, because the cleanup
    /// also retires directories left behind by a restart.
    /// </summary>
    /// <param name="jobId">The job to retire.</param>
    public void Retire(Guid jobId) => jobStore.RemoveJob(jobId);

    /// <summary>
    /// Creates the delivery of a job whose start carried a <see cref="DeliveryIntent"/>, on the caller's behalf.
    /// Nothing else answers that caller, who polls the attempt, so a refusal or an error is recorded on the job and in
    /// the execution protocol instead of only being logged.
    /// </summary>
    private Task CreateDeliveryAsync(Guid jobId, DeliveryIntent deliveryIntent, CancellationToken cancellationToken) =>
        CarryOutAsync(jobId, "create the delivery", LogLevel.Warning, async services =>
        {
            DeliveryCreationResult result;
            try
            {
                result = await services.GetRequiredService<IDeliveryCreationService>()
                    .CreateAsync(jobId, deliveryIntent.Fields, deliveryIntent.Supplier, cancellationToken);
            }
            catch (Exception ex)
            {
                // Otherwise the attempt would report a delivery in progress until the job is retired.
                await RecordDeliveryFailureAsync(jobId, DeliveryCreationResult.UnexpectedFailureDescription);
                logger.LogError(ex, "Creating the delivery for job <{JobId}> failed unexpectedly.", jobId);
                return;
            }

            switch (result.Status)
            {
                case DeliveryCreationStatus.Created:
                    logger.LogInformation("Created delivery <{DeliveryId}> for job <{JobId}>.", result.DeliveryId, jobId);
                    break;

                case DeliveryCreationStatus.AlreadyDelivered:
                    logger.LogInformation("Job <{JobId}> was already delivered as <{DeliveryId}>, so the attempt adopts it.", jobId, result.DeliveryId);
                    break;

                case DeliveryCreationStatus.JobNotDeliverable:
                    // Data that did not pass: the run's own state already says so, and the attempt reports it as
                    // rejected rather than as a failed creation.
                    logger.LogInformation("Job <{JobId}> did not allow a delivery, so none was created.", jobId);
                    break;

                default:
                    await RecordDeliveryFailureAsync(jobId, result.DescribeFailure());
                    logger.LogWarning("Could not create the delivery for job <{JobId}>: {Status}.", jobId, result.Status);
                    break;
            }
        });

    /// <summary>
    /// Records why geopilot could not create the delivery: on the job, for the caller polling the attempt, and in the
    /// execution protocol, which keeps it beyond the job's retention. The protocol write takes a scope of its own,
    /// so it does not save along whatever the failed creation left in its database context.
    /// </summary>
    private async Task RecordDeliveryFailureAsync(Guid jobId, string reason)
    {
        jobStore.RecordDeliveryFailure(jobId, reason);
        await RecordProtocolAsync(jobId, recorder => recorder.RecordDeliveryFailedAsync(jobId, reason));
    }

    /// <summary>
    /// Runs one protocol write. The recorder swallows its own write failures; the guard additionally covers
    /// resolving it.
    /// </summary>
    private Task RecordProtocolAsync(Guid jobId, Func<IRecorder, Task> record) =>
        CarryOutAsync(jobId, "write the execution protocol", LogLevel.Warning, services => record(services.GetRequiredService<IRecorder>()));

    /// <summary>
    /// Carries out one consequence against a fresh scope, because the services behind it depend on the scoped
    /// database context. Logs a failure instead of throwing, so it neither skips the consequences after it nor
    /// reaches the loop that reported the event.
    /// </summary>
    private async Task CarryOutAsync(Guid jobId, string consequence, LogLevel failureLevel, Func<IServiceProvider, Task> carryOut)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            await carryOut(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            logger.Log(failureLevel, ex, "Could not {Consequence} for job <{JobId}>.", consequence, jobId);
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
    private async Task ReleaseUploadAsync(Guid jobId, Guid? uploadId)
    {
        if (uploadId is not { } id)
            return;

        await CarryOutAsync(jobId, "release the upload", LogLevel.Error, services =>
            services.GetRequiredService<IUploadOrchestrationService>().ReleaseUploadAsync(id));
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
