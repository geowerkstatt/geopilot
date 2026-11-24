namespace Geopilot.Api;

/// <summary>
/// Reads the times the API takes.
/// </summary>
internal static class DateTimeExtensions
{
    /// <summary>
    /// The time as UTC. The API documents its times as UTC: a time without offset binds as unspecified and is read as
    /// UTC, not in the time zone of the host. The database compares timestamps with time zone and takes only UTC values.
    /// </summary>
    internal static DateTime AsUtc(this DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
}
