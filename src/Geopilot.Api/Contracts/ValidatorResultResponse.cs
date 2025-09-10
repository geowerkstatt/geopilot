using Geopilot.Api.Validation;

namespace Geopilot.Api.Contracts;

/// <summary>
/// The DTO for a validator result.
/// </summary>
public record class ValidatorResultResponse(Status Status, string? StatusMessage, IDictionary<string, string> LogFiles);
