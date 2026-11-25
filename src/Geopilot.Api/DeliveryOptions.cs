using Cronos;
using Geopilot.Api.Models;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

namespace Geopilot.Api;

/// <summary>
/// Configuration options for deliveries.
/// </summary>
public class DeliveryOptions : IValidatableObject
{
    // How far back to look for the last occurrence of the interval, shortest first.
    private static readonly TimeSpan[] IntervalLookBacks = [TimeSpan.FromHours(1), TimeSpan.FromDays(1), TimeSpan.FromDays(32), TimeSpan.FromDays(367), TimeSpan.FromDays(5 * 366)];

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
    /// Gets when an uploader can no longer delete a delivery created at <paramref name="deliveredAt"/>, in UTC.
    /// Without uploader deletion that is the creation itself, and <see langword="null"/> means the window never closes.
    /// </summary>
    /// <param name="deliveredAt">When the delivery was created; a value without <see cref="DateTimeKind"/> counts as UTC.</param>
    public DateTime? GetDeleteWindowEnd(DateTime deliveredAt)
    {
        var deliveredAtUtc = deliveredAt.Kind switch
        {
            DateTimeKind.Utc => deliveredAt,
            DateTimeKind.Local => deliveredAt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(deliveredAt, DateTimeKind.Utc),
        };

        if (!UploaderDeleteEnabled)
        {
            return deliveredAtUtc;
        }

        var durationEnd = deliveredAtUtc + DeleteDuration;
        var intervalEnd = DeleteRestrictIntervalExpression?.GetNextOccurrence(deliveredAtUtc, TimeZone);

        // Min skips null: the earlier bound wins, and without any bound the window stays open.
        return new[] { durationEnd, intervalEnd }.Min();
    }

    /// <summary>
    /// Gets a condition that holds for every delivery an uploader can no longer delete at <paramref name="utcNow"/>, by the
    /// rule of <see cref="GetDeleteWindowEnd"/>, in a form a database query can apply. A window that never closes never
    /// matches.
    /// </summary>
    /// <param name="utcNow">The moment to check, in UTC.</param>
    public Expression<Func<Delivery, bool>> DeleteWindowClosedAt(DateTime utcNow)
    {
        if (!UploaderDeleteEnabled)
        {
            return delivery => delivery.Date <= utcNow;
        }

        // The duration has passed for a delivery older than it, the interval once it occurred after the delivery.
        var byDuration = DeleteDuration.HasValue;
        var durationCutoff = utcNow - DeleteDuration.GetValueOrDefault();
        var lastOccurrence = DeleteRestrictIntervalExpression is { } interval ? GetLastOccurrence(interval, utcNow) : null;
        var byInterval = lastOccurrence.HasValue;
        var intervalCutoff = lastOccurrence.GetValueOrDefault(utcNow);
        return delivery => (byDuration && delivery.Date <= durationCutoff) || (byInterval && delivery.Date < intervalCutoff);
    }

    // shortcut: looks back five years at most, so an interval that occurs less often closes no window here.
    private DateTime? GetLastOccurrence(CronExpression interval, DateTime utcNow)
    {
        // Cronos only looks forward, so search back in growing steps rather than listing years of a frequent interval.
        foreach (var lookBack in IntervalLookBacks)
        {
            DateTime? last = null;
            foreach (var occurrence in interval.GetOccurrences(utcNow - lookBack, utcNow, TimeZone, fromInclusive: true, toInclusive: true))
            {
                last = occurrence;
            }

            if (last is not null)
            {
                return last;
            }
        }

        return null;
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
