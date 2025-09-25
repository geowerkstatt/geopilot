using Geopilot.Api.Validation;

namespace Geopilot.Api.Contracts;

/// <summary>
/// The DTO for a validation job.
/// </summary>
public record ValidationJobResponse(Guid JobId, Status Status, IDictionary<string, ValidatorResultResponse?> ValidatorResults);
