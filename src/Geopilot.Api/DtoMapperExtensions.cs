using Geopilot.Api.Contracts;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Processing;

namespace Api;

/// <summary>
/// Provides extension methods for mapping domain models to DTOs.
/// </summary>
internal static class DtoMapperExtensions
{
    /// <summary>
    /// Maps a <see cref="ProcessingJob"/> to a <see cref="ProcessingJobResponse"/>.
    /// </summary>
    /// <param name="job">The processing job to map.</param>
    /// <param name="buildDownloadUrl">Builds an absolute download URL for a (jobId, fileName) pair.</param>
    /// <param name="pipelineConfig">
    /// Optional pipeline definition used to fill in display names + steps when the job has not yet
    /// instantiated a live <see cref="ProcessingJob.Pipeline"/> (e.g. cloud upload between PATCH and
    /// preflight completion). All synthesized steps are reported as <see cref="StepState.Pending"/>.
    /// </param>
    public static ProcessingJobResponse ToResponse(this ProcessingJob job, Func<Guid, string, Uri> buildDownloadUrl, PipelineConfig? pipelineConfig = null)
    {
        var state = job.IsFailed
            ? ProcessingState.Failed
            : job.Pipeline?.State ?? ProcessingState.Pending;

        var pipelineName = job.Pipeline?.DisplayName
            ?? pipelineConfig?.DisplayName
            ?? new Dictionary<string, string>();

        var steps = job.Pipeline?.Steps
            .Select(step => step.ToResponse(job.Id, buildDownloadUrl))
            .ToList()
            ?? pipelineConfig?.Steps
                .Select(stepConfig => new StepResultResponse(
                    stepConfig.Id,
                    stepConfig.DisplayName,
                    StepState.Pending,
                    null,
                    new List<StepDownload>()))
                .ToList()
            ?? new List<StepResultResponse>();

        return new ProcessingJobResponse(
            job.Id,
            state,
            job.MandateId,
            pipelineName,
            steps,
            job.Pipeline?.DeliveryRestrictionMessage);
    }

    /// <summary>
    /// Maps a single <see cref="IPipelineStep"/> to a <see cref="StepResultResponse"/>.
    /// </summary>
    private static StepResultResponse ToResponse(this IPipelineStep step, Guid jobId, Func<Guid, string, Uri> buildDownloadUrl)
    {
        var downloads = step.Downloads
            .Select(pd => new StepDownload(
                pd.OriginalFileName,
                buildDownloadUrl(jobId, pd.PersistedFileName)))
            .ToList();

        return new StepResultResponse(
            step.Id,
            step.DisplayName,
            step.State,
            step.StatusMessage,
            downloads);
    }
}
