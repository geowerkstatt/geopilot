using System.Diagnostics.CodeAnalysis;

namespace GeoCop.Api.Validation
{
    /// <summary>
    /// Represents the result of one validation as part of a validation job.
    /// </summary>
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1313:ParameterNamesMustBeginWithLowerCaseLetter", Justification = "Record class constructor.")]
    public record class ValidatorResult(Status Status, string? StatusMessage)
    {
        /// <summary>
        /// Available log files to download.
        /// </summary>
        public IDictionary<string, string> LogFiles { get; init; } = new Dictionary<string, string>();
    }
}
