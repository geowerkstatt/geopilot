namespace Geopilot.Api.Validation;

/// <summary>
/// Represents the result of one validation as part of a validation job.
/// </summary>
public record class ValidatorResult(ValidatorResultStatus Status, string? StatusMessage)
{
    /// <summary>
    /// Available log files to download.
    /// </summary>
    public IDictionary<string, string> LogFiles { get; init; } = new Dictionary<string, string>();
}
