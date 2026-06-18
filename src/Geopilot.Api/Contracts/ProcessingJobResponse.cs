using Geopilot.Pipeline;
using Geopilot.PipelineCore.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// The DTO for a processing job.
/// </summary>
/// <param name="JobId">The unique id of the processing job.</param>
/// <param name="State">The aggregated state of the underlying processing pipeline.</param>
/// <param name="MandateId">The id of the mandate the job is running for, or <see langword="null"/> if no mandate has been associated yet.</param>
/// <param name="PipelineName">The localized display name of the pipeline. Empty if no pipeline has been associated yet.</param>
/// <param name="Steps">The result of each pipeline step, in execution order.</param>
/// <param name="DeliveryRestrictionMessage">Localized message describing why delivery is prevented after a successful run. <see langword="null"/> when delivery is allowed.</param>
public record ProcessingJobResponse(
    Guid JobId,
    ProcessingState State,
    int? MandateId,
    LocalizedText PipelineName,
    IList<StepResultResponse> Steps,
    LocalizedText? DeliveryRestrictionMessage);
