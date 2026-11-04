using Cronos;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Api;

/// <summary>
/// Configuration options for deliveries.
/// </summary>
public class DeliveryOptions : IValidatableObject
{
    private string? deleteRestrictInterval;

    /// <summary>
    /// Whether users are allowed to delete their own uploaded deliveries.
    /// Restrictions can be defined using <see cref="DeleteDuration"/> and <see cref="DeleteRestrictInterval"/>.
    /// </summary>
    public bool UploaderDeleteEnabled { get; set; }

    /// <summary>
    /// The time period during which users are allowed to delete their own uploaded deliveries.
    /// </summary>
    public TimeSpan? DeleteDuration { get; set; }

    /// <summary>
    /// A cron expression for a time interval after which deleting deliveries is restricted,
    /// e.g. "0 0 * * *" to restrict deletions every day at midnight. It is read in the server's time zone.
    /// </summary>
    public string? DeleteRestrictInterval
    {
        get => deleteRestrictInterval;
        set
        {
            deleteRestrictInterval = value;
            DeleteRestrictIntervalExpression = !string.IsNullOrEmpty(value) && CronExpression.TryParse(value, out var expression) ? expression : null;
        }
    }

    /// <summary>
    /// The parsed cron expression for <see cref="DeleteRestrictInterval"/>, <see langword="null"/> when it is unset or malformed.
    /// </summary>
    public CronExpression? DeleteRestrictIntervalExpression { get; private set; }

    /// <summary>
    /// The time zone <see cref="DeleteRestrictInterval"/> is read in. Always the server's; tests set it to stay
    /// independent of the machine they run on.
    /// </summary>
    internal TimeZoneInfo TimeZone { get; init; } = TimeZoneInfo.Local;

    /// <summary>
    /// Gets when an uploader can no longer delete a delivery declared at <paramref name="declaredAt"/>, in UTC.
    /// Without uploader deletion that is the declaration itself, and <see langword="null"/> means the window never closes.
    /// </summary>
    /// <param name="declaredAt">When the delivery was declared; a value without <see cref="DateTimeKind"/> counts as UTC.</param>
    public DateTime? GetDeleteWindowEnd(DateTime declaredAt)
    {
        var declaredAtUtc = declaredAt.Kind switch
        {
            DateTimeKind.Utc => declaredAt,
            DateTimeKind.Local => declaredAt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(declaredAt, DateTimeKind.Utc),
        };

        if (!UploaderDeleteEnabled)
        {
            return declaredAtUtc;
        }

        var durationEnd = declaredAtUtc + DeleteDuration;
        var intervalEnd = DeleteRestrictIntervalExpression?.GetNextOccurrence(declaredAtUtc, TimeZone);

        // Min skips null: the earlier bound wins, and without any bound the window stays open.
        return new[] { durationEnd, intervalEnd }.Min();
    }

    /// <summary>
    /// Whether the window in which uploaders may delete a delivery closes. A mandate needs that to choose a reaction set,
    /// because its reactions only run once the window has closed.
    /// </summary>
    public bool DeleteWindowCloses => GetDeleteWindowEnd(DateTime.UtcNow) is not null;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DeleteDuration <= TimeSpan.Zero)
        {
            yield return new ValidationResult($"The delete duration must be positive, but is {DeleteDuration}.", [nameof(DeleteDuration)]);
        }

        if (!string.IsNullOrEmpty(DeleteRestrictInterval) && DeleteRestrictIntervalExpression is null)
        {
            yield return new ValidationResult(
                $"The delete restrict interval '{DeleteRestrictInterval}' is not a cron expression with five fields such as \"0 0 * * *\".",
                [nameof(DeleteRestrictInterval)]);
        }
    }
}
