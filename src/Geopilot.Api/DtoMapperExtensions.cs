using Geopilot.Api.Contracts;
using Geopilot.Api.Models;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api;

/// <summary>
/// Provides extension methods for mapping domain models to DTOs.
/// </summary>
internal static class DtoMapperExtensions
{
    /// <summary>
    /// Id of the synthetic preflight step that always leads the reported steps.
    /// </summary>
    internal const string PreflightStepId = "preflight";

    private static readonly LocalizedText PreflightStepName = new(new Dictionary<string, string>
    {
        ["de"] = "Vorbereitung",
        ["en"] = "Preparation",
        ["fr"] = "Préparation",
        ["it"] = "Preparazione",
    });

    /// <summary>
    /// Message shown when preflight fails. <c>{jobId}</c> is replaced with the job id so support can trace the run.
    /// </summary>
    private static readonly LocalizedText PreflightFailedMessageTemplate = new(new Dictionary<string, string>
    {
        ["de"] = "Beim Starten der Verarbeitung ist ein Fehler aufgetreten. Möglicherweise liegt ein Problem mit den hochgeladenen Dateien oder ein Serverfehler vor. Bitte versuchen Sie es später erneut und benachrichtigen Sie den Support, falls das Problem weiterhin besteht. Referenz für den Support: {jobId}.",
        ["en"] = "An error occurred while starting the processing. This may be caused by a problem with the uploaded files or by a server error. Please try again later and notify support if the problem persists. Reference for support: {jobId}.",
        ["fr"] = "Une erreur s'est produite lors du démarrage du traitement. Cela peut être dû à un problème avec les fichiers téléversés ou à une erreur du serveur. Veuillez réessayer plus tard et contacter le support si le problème persiste. Référence pour le support: {jobId}.",
        ["it"] = "Si è verificato un errore durante l'avvio dell'elaborazione. Potrebbe essere dovuto a un problema con i file caricati o a un errore del server. Riprova più tardi e contatta il supporto se il problema persiste. Riferimento per il supporto: {jobId}.",
    });

    /// <summary>
    /// Maps a <see cref="ProcessingJob"/> to a <see cref="ProcessingJobResponse"/>.
    /// </summary>
    /// <param name="job">The processing job to map.</param>
    /// <param name="buildDownloadUrl">Builds an absolute download URL for a (jobId, fileName) pair.</param>
    /// <param name="buildVisualizationUrl">Builds an absolute visualization-config URL for a (jobId, fileName) pair.</param>
    public static ProcessingJobResponse ToResponse(this ProcessingJob job, Func<Guid, string, Uri> buildDownloadUrl, Func<Guid, string, Uri> buildVisualizationUrl)
    {
        var pipelineName = job.Pipeline?.DisplayName ?? LocalizedText.Empty;

        var steps = new List<StepResultResponse> { BuildPreflightStep(job) };
        if (job.Pipeline != null)
        {
            steps.AddRange(job.Pipeline.Steps.Select(step => step.ToResponse(job.Id, buildDownloadUrl, buildVisualizationUrl)));
        }

        return new ProcessingJobResponse(
            job.Id,
            job.State,
            job.MandateId,
            pipelineName,
            steps);
    }

    /// <summary>
    /// Builds the synthetic preflight step that always leads the reported steps. When preflight failed a
    /// generic status message is attached so the user sees that preparation, not a pipeline step, failed.
    /// </summary>
    private static StepResultResponse BuildPreflightStep(ProcessingJob job)
    {
        var state = DerivePreflightState(job);
        var statusMessage = state == StepState.Error
            ? PreflightFailedMessageTemplate.Map(text => text.Replace("{jobId}", job.Id.ToString(), StringComparison.Ordinal))
            : null;

        return new StepResultResponse(
            PreflightStepId,
            PreflightStepName,
            state,
            statusMessage,
            ConditionMessage: null,
            Downloads: new List<StepDownload>(),
            Visualizations: new List<StepVisualizationResponse>());
    }

    /// <summary>
    /// Derives the preflight step's state from the job's lifecycle. Preflight runs while the job is still
    /// pending and succeeds once the pipeline has been started (the job left the pending state). A failure
    /// counts as a preflight failure only when the pipeline never started; a failure that occurs once the
    /// pipeline is running is attributed to the pipeline itself, so preflight is reported as succeeded.
    /// </summary>
    private static StepState DerivePreflightState(ProcessingJob job) => job.State switch
    {
        ProcessingState.Pending => StepState.Running,
        ProcessingState.Running => StepState.Success,
        ProcessingState.Success => StepState.Success,
        ProcessingState.Warning => StepState.Success,
        ProcessingState.DeliveryRestriction => StepState.Success,
        ProcessingState.Cancelled => StepState.Success,
        ProcessingState.Failed => job.Pipeline is null || job.Pipeline.State == ProcessingState.Pending
            ? StepState.Error
            : StepState.Success,
        _ => StepState.Pending,
    };

    /// <summary>
    /// Maps a single <see cref="IPipelineStep"/> to a <see cref="StepResultResponse"/>.
    /// </summary>
    private static StepResultResponse ToResponse(this IPipelineStep step, Guid jobId, Func<Guid, string, Uri> buildDownloadUrl, Func<Guid, string, Uri> buildVisualizationUrl)
    {
        var downloads = step.Downloads
            .Select(pd => new StepDownload(
                pd.OriginalFileName,
                buildDownloadUrl(jobId, pd.PersistedFileName)))
            .ToList();

        var visualizations = step.Visualizations
            .Select(v => new StepVisualizationResponse(
                v.OriginalFileName,
                buildVisualizationUrl(jobId, v.PersistedFileName)))
            .ToList();

        return new StepResultResponse(
            step.Id,
            step.DisplayName,
            step.State,
            step.StatusMessage,
            step.ConditionMessage,
            downloads,
            visualizations);
    }

    /// <summary>
    /// Maps a <see cref="Mandate"/> to a <see cref="MandateSummary"/>.
    /// </summary>
    public static MandateSummary ToSummary(this Mandate mandate)
    {
        return new MandateSummary(
            mandate.Id,
            mandate.Name,
            mandate.Description,
            mandate.AllowDelivery,
            mandate.EvaluatePrecursorDelivery,
            mandate.EvaluatePartial,
            mandate.EvaluateComment);
    }

    /// <summary>
    /// Maps the <see cref="Mandate"/> entries to <see cref="MandateSummary"/>.
    /// </summary>
    public static IQueryable<MandateSummary> ToSummaries(this IQueryable<Mandate> mandates)
    {
        return mandates.Select(m => new MandateSummary(
            m.Id,
            m.Name,
            m.Description,
            m.AllowDelivery,
            m.EvaluatePrecursorDelivery,
            m.EvaluatePartial,
            m.EvaluateComment));
    }

    /// <summary>
    /// Maps the <see cref="Delivery"/> entries to <see cref="DeliverySummary"/>.
    /// </summary>
    public static IQueryable<DeliverySummary> ToSummaries(this IQueryable<Delivery> deliveries)
    {
        return deliveries.Select(d => new DeliverySummary(d.Id, d.Date));
    }
}
