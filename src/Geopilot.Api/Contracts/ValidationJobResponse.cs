using Geopilot.Api.Validation;

namespace Geopilot.Api.Contracts;

public record ValidationJobResponse(Guid JobId, Status Status, IDictionary<string, ValidatorResultResponse> ValidatorResults);
