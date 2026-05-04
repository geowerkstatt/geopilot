using Geopilot.Api.Contracts;
using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Processing;
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
                    new Dictionary<string, Uri>()))
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
        var statusMessage = ExtractStatusMessage(step);

        var downloads = step.PersistedDownloads.ToDictionary(
            kvp => kvp.Key,
            kvp => buildDownloadUrl(jobId, kvp.Value));

        return new StepResultResponse(
            step.Id,
            step.DisplayName,
            step.State,
            statusMessage,
            downloads);
    }

    /// <summary>
    /// Merges all <see cref="OutputAction.StatusMessage"/> outputs from the step into a single localized dict
    /// (per language, separate messages joined with " - ").
    /// </summary>
    private static Dictionary<string, string>? ExtractStatusMessage(IPipelineStep step)
    {
        if (step.Result == null)
            return null;

        var localizedMessages = step.Result.Outputs
            .Where(o => o.Value.Action.Contains(OutputAction.StatusMessage))
            .Select(o => o.Value.Data as Dictionary<string, string>)
            .Where(m => m != null)
            .Cast<Dictionary<string, string>>()
            .ToList();

        if (localizedMessages.Count == 0)
            return null;

        var languages = localizedMessages.SelectMany(m => m.Keys).Distinct();
        var merged = new Dictionary<string, string>();
        foreach (var language in languages)
        {
            var parts = localizedMessages.Where(m => m.ContainsKey(language)).Select(m => m[language]);
            merged[language] = string.Join(" - ", parts);
        }

        return merged;
    }
}
