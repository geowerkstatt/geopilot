using System.Collections.Immutable;

namespace Geopilot.Api.Validation;

/// <summary>
/// Represents a validation job.
/// </summary>
public record class ValidationJob(
    Guid Id,
    string? OriginalFileName,
    string? TempFileName,
    ImmutableDictionary<string, ValidatorResult?> ValidatorResults,
    Status Status)
{
    /// <summary>
    /// Aggregates the status of all <see cref="ValidatorResults"/> and returns the aggregated <see cref="Status"/>.
    /// </summary>
    public static Status GetStatusFromResults(ImmutableDictionary<string, ValidatorResult?> validatorResults)
    {
        if (validatorResults == null || validatorResults.Count == 0)
            throw new ArgumentException("Validator results must not be null or empty.", nameof(validatorResults));

        if (validatorResults.Values.Any(v => v is null))
            return Status.Processing;

        var statuses = validatorResults.Values.Select(v => v!.Status).ToList();

        if (statuses.Contains(ValidatorResultStatus.Failed))
            return Status.Failed;

        if (statuses.Contains(ValidatorResultStatus.CompletedWithErrors))
            return Status.CompletedWithErrors;

        return Status.Completed;
    }
}
