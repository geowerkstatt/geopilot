using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Geopilot.Api;

[TestClass]
public class DeliveryOptionsTest
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    [TestMethod]
    [DataRow("0 0 * * *", "2026-01-15T10:00:00Z", "2026-01-15T23:00:00Z", DisplayName = "Midnight in winter")]
    [DataRow("0 0 * * *", "2026-07-15T10:00:00Z", "2026-07-15T22:00:00Z", DisplayName = "Midnight in summer")]
    [DataRow("0 12 * * *", "2026-07-15T08:00:00Z", "2026-07-15T10:00:00Z", DisplayName = "Noon in summer")]
    [DataRow("0 0 * * *", "2026-03-28T12:00:00Z", "2026-03-28T23:00:00Z", DisplayName = "Day before the switch to summer time")]
    [DataRow("0 0 * * *", "2026-03-29T10:00:00Z", "2026-03-29T22:00:00Z", DisplayName = "Day of the switch to summer time")]
    [DataRow("0 0 * * *", "2026-10-24T10:00:00Z", "2026-10-24T22:00:00Z", DisplayName = "Day before the switch to winter time")]
    [DataRow("0 0 * * *", "2026-10-25T10:00:00Z", "2026-10-25T23:00:00Z", DisplayName = "Day of the switch to winter time")]
    public void GetDeleteWindowEndReadsIntervalInLocalTime(string interval, string declaredAt, string expectedEnd)
    {
        var options = new DeliveryOptions { UploaderDeleteEnabled = true, DeleteRestrictInterval = interval, TimeZone = Zurich };

        Assert.AreEqual(ParseUtc(expectedEnd), options.GetDeleteWindowEnd(ParseUtc(declaredAt)));
    }

    [TestMethod]
    [DataRow("2026-01-15T10:00:00Z", "2026-01-15T12:00:00Z", DisplayName = "Duration ends first")]
    [DataRow("2026-01-15T22:00:00Z", "2026-01-15T23:00:00Z", DisplayName = "Interval ends first")]
    public void GetDeleteWindowEndTakesTheEarlierBound(string declaredAt, string expectedEnd)
    {
        var options = new DeliveryOptions
        {
            UploaderDeleteEnabled = true,
            DeleteDuration = TimeSpan.FromHours(2),
            DeleteRestrictInterval = "0 0 * * *",
            TimeZone = Zurich,
        };

        Assert.AreEqual(ParseUtc(expectedEnd), options.GetDeleteWindowEnd(ParseUtc(declaredAt)));
    }

    [TestMethod]
    public void GetDeleteWindowEndAddsDuration()
    {
        var options = new DeliveryOptions { UploaderDeleteEnabled = true, DeleteDuration = TimeSpan.FromMinutes(90), TimeZone = Zurich };

        Assert.AreEqual(ParseUtc("2026-07-15T11:30:00Z"), options.GetDeleteWindowEnd(ParseUtc("2026-07-15T10:00:00Z")));
    }

    [TestMethod]
    public void GetDeleteWindowEndIsTheDeclarationWithoutUploaderDeletion()
    {
        var options = new DeliveryOptions { UploaderDeleteEnabled = false, DeleteDuration = TimeSpan.FromHours(2), DeleteRestrictInterval = "0 0 * * *" };

        Assert.AreEqual(ParseUtc("2026-07-15T10:00:00Z"), options.GetDeleteWindowEnd(ParseUtc("2026-07-15T10:00:00Z")));
    }

    [TestMethod]
    public void GetDeleteWindowEndIsUnboundedWithoutDurationAndInterval()
    {
        var options = new DeliveryOptions { UploaderDeleteEnabled = true };

        Assert.IsNull(options.GetDeleteWindowEnd(ParseUtc("2026-07-15T10:00:00Z")));
    }

    [TestMethod]
    public void GetDeleteWindowEndReadsUnspecifiedKindAsUtc()
    {
        var options = new DeliveryOptions { UploaderDeleteEnabled = true, DeleteDuration = TimeSpan.FromHours(1) };
        var declaredAt = DateTime.SpecifyKind(new DateTime(2026, 7, 15, 10, 0, 0), DateTimeKind.Unspecified);

        var windowEnd = options.GetDeleteWindowEnd(declaredAt);

        Assert.AreEqual(ParseUtc("2026-07-15T11:00:00Z"), windowEnd);
        Assert.AreEqual(DateTimeKind.Utc, windowEnd?.Kind);
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("01:00:00", "")]
    [DataRow(null, "0 0 * * *")]
    public void ValidateAcceptsValidSettings(string? duration, string? interval)
    {
        var options = new DeliveryOptions
        {
            UploaderDeleteEnabled = true,
            DeleteDuration = duration == null ? null : TimeSpan.Parse(duration, CultureInfo.InvariantCulture),
            DeleteRestrictInterval = interval,
        };

        Assert.IsEmpty(Validate(options));
    }

    [TestMethod]
    [DataRow("00:00:00")]
    [DataRow("-01:00:00")]
    public void ValidateRejectsDurationThatIsNotPositive(string duration)
    {
        var options = new DeliveryOptions { UploaderDeleteEnabled = true, DeleteDuration = TimeSpan.Parse(duration, CultureInfo.InvariantCulture) };

        var result = Assert.ContainsSingle(Validate(options));
        CollectionAssert.AreEqual(new[] { nameof(DeliveryOptions.DeleteDuration) }, result.MemberNames.ToArray());
    }

    [TestMethod]
    [DataRow("0 0 * *")]
    [DataRow("daily")]
    public void ValidateRejectsMalformedInterval(string interval)
    {
        var options = new DeliveryOptions { UploaderDeleteEnabled = true, DeleteRestrictInterval = interval };

        var result = Assert.ContainsSingle(Validate(options));
        CollectionAssert.AreEqual(new[] { nameof(DeliveryOptions.DeleteRestrictInterval) }, result.MemberNames.ToArray());
        Assert.Contains(interval, result.ErrorMessage);
    }

    private static DateTime ParseUtc(string value)
        => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    private static List<ValidationResult> Validate(DeliveryOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
