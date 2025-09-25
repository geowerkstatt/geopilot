using System.Collections.Immutable;

namespace Geopilot.Api.Validation;

/// <summary>
/// Represents the result of one validation by an <see cref="IValidator"/> as part of a validation job.
/// </summary>
public record class ValidatorResult(
    ValidatorResultStatus Status,
    string? StatusMessage,
    ImmutableDictionary<string, string> LogFiles)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatorResult"/> class with the specified status and status message.
    /// </summary>
    /// <param name="status">The status of the validation result.</param>
    /// <param name="statusMessage">An optional message providing additional information about the validation status. Can be <see langword="null"/>.</param>
    public ValidatorResult(ValidatorResultStatus status, string? statusMessage)
        : this(status, statusMessage, ImmutableDictionary<string, string>.Empty)
    {
    }
}
