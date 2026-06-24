using Cronos;

namespace Geopilot.Api;

/// <summary>
/// Configuration options for deliveries.
/// </summary>
public class DeliveryOptions
{
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
    /// e.g. "0 0 * * *" to restrict deletions every day at midnight.
    /// </summary>
    public string? DeleteRestrictInterval
    {
        get => DeleteRestrictIntervalExpression?.ToString();
        set => DeleteRestrictIntervalExpression = string.IsNullOrEmpty(value) ? null : CronExpression.Parse(value);
    }

    /// <summary>
    /// The parsed cron expression for <see cref="DeleteRestrictInterval"/>.
    /// </summary>
    public CronExpression? DeleteRestrictIntervalExpression { get; set; }
}
