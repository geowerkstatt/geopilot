using System.Diagnostics.CodeAnalysis;

namespace Geopilot.Api.Validation;

/// <summary>
/// Represents the status and validator results of a validation job.
/// </summary>
/// <param name="JobId">The job identification.</param>
[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1313:ParameterNamesMustBeginWithLowerCaseLetter", Justification = "Record class constructor.")]
public record class ValidationJobStatus(Guid JobId)
{
    /// <summary>
    /// Overall status of the validation job.
    /// </summary>
    public Status Status { get; set; } = Status.Processing;

    /// <summary>
    /// Available validator results.
    /// </summary>
    public IDictionary<string, ValidatorResult> ValidatorResults { get; } = new Dictionary<string, ValidatorResult>();

    /// <summary>
    /// Aggregates the status of all <see cref="ValidatorResults"/> and updates the <see cref="Status"/> property.
    /// </summary>
    public void UpdateJobStatusFromResults()
    {
        Status = ValidatorResults.Values
            .Select(v => v.Status)
            .Aggregate(Status.Completed, ReduceStatus);
    }

    private static Status ReduceStatus(Status current, Status next)
    {
        return current switch
        {
            // Completed has the lowest priority and requires all results to be completed.
            Status.Completed => next,

            // CompletedWithErrors has a higher priority than completed.
            Status.CompletedWithErrors => next == Status.Completed ? Status.CompletedWithErrors : next,

            // Failed is only set when all validations finished.
            Status.Failed => next == Status.Processing ? next : Status.Failed,

            // Processing has the highest priority. If at least one validator is still running, the job is processing.
            Status.Processing => Status.Processing,

            _ => throw new InvalidOperationException($"Unknown status <{current}>."),
        };
    }
}
