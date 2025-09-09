using Geopilot.Api.Validation;

namespace Geopilot.Api.Contracts;

public record class ValidatorResultResponse(Status Status, string? StatusMessage, IDictionary<string, string> LogFiles);
