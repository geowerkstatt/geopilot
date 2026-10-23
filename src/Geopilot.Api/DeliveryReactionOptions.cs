using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api;

/// <summary>
/// Configuration options for running the reactions to deliveries.
/// </summary>
public class DeliveryReactionOptions : IValidatableObject
{
    /// <summary>
    /// The configuration section these options are bound to.
    /// </summary>
    public const string SectionName = "Delivery:Reactions";

    /// <summary>
    /// The waits between the starts of the attempts of a reaction when <see cref="RetryDelays"/> is not configured:
    /// seven attempts, the last one almost 21 hours after the first.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> DefaultRetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
    ];

    /// <summary>
    /// The wait from the start of each failed attempt of a reaction to the start of the next one; after the last
    /// attempt it gives up. While the application runs, a reaction is therefore done or given up at the latest when its
    /// hold ends plus the sum of the waits plus the run of its last attempt. Null or empty means
    /// <see cref="DefaultRetryDelays"/>.
    /// </summary>
    // Null rather than the default: the configuration binder appends to a preset list instead of replacing it.
    public IReadOnlyList<TimeSpan>? RetryDelays { get; set; }

    /// <summary>
    /// How long a single attempt of a reaction may take.
    /// </summary>
    public TimeSpan TimeLimit { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How often an instance looks for reactions that are due.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The waits between the attempts that apply.
    /// </summary>
    internal IReadOnlyList<TimeSpan> EffectiveRetryDelays => RetryDelays is { Count: > 0 } ? RetryDelays : DefaultRetryDelays;

    /// <summary>
    /// How many attempts a reaction gets before it gives up.
    /// </summary>
    internal int MaxAttempts => EffectiveRetryDelays.Count + 1;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TimeLimit <= TimeSpan.Zero)
        {
            yield return new ValidationResult($"The time limit must be positive, but is {TimeLimit}.", [nameof(TimeLimit)]);
        }

        if (PollInterval <= TimeSpan.Zero)
        {
            yield return new ValidationResult($"The poll interval must be positive, but is {PollInterval}.", [nameof(PollInterval)]);
        }

        if (RetryDelays?.Any(delay => delay <= TimeSpan.Zero) == true)
        {
            yield return new ValidationResult("Every retry delay must be positive.", [nameof(RetryDelays)]);
        }
    }
}
