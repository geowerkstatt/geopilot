using Geopilot.Api.Contracts;
using Geopilot.Api.Processing;
using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;

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
    public static ProcessingJobResponse ToResponse(this ProcessingJob job, Func<Guid, string, Uri> buildDownloadUrl)
    {
        var pipelineName = job.Pipeline?.DisplayName ?? LocalizedText.Empty;

        var steps = job.Pipeline?.Steps
            .Select(step => step.ToResponse(job.Id, buildDownloadUrl))
            .ToList()
            ?? new List<StepResultResponse>();

        return new ProcessingJobResponse(
            job.Id,
            job.State,
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

        var visualizations = step.Visualizations
            .Select(v => new StepVisualizationResponse(
                v.Kind,
                v.OriginalFileName,
                buildDownloadUrl(jobId, v.PersistedFileName)))
            .ToList();

        return new StepResultResponse(
            step.Id,
            step.DisplayName,
            step.State,
            step.StatusMessage,
            downloads,
            visualizations);
    }
}
