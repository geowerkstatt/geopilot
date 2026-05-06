using Geopilot.Api.Pipeline;

namespace Geopilot.Api.Contracts;

/// <summary>
/// The DTO for a processing job.
/// </summary>
/// <param name="JobId">The unique id of the processing job.</param>
/// <param name="State">The aggregated state of the underlying processing pipeline.</param>
/// <param name="MandateId">The id of the mandate the job is running for, or <see langword="null"/> if no mandate has been associated yet.</param>
/// <param name="PipelineName">The pipeline's display name, keyed by ISO 639 language code. Empty if no pipeline has been associated yet.</param>
/// <param name="Steps">The result of each pipeline step, in execution order.</param>
/// <param name="DeliveryRestrictionMessage">Localized message (key: ISO 639 language code) describing why delivery is prevented after a successful run. <see langword="null"/> when delivery is allowed.</param>
public record ProcessingJobResponse(
    Guid JobId,
    ProcessingState State,
    int? MandateId,
    IDictionary<string, string> PipelineName,
    IList<StepResultResponse> Steps,
    IDictionary<string, string>? DeliveryRestrictionMessage);
