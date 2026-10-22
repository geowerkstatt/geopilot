using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Geopilot.Api;

[TestClass]
public class DeliveryReactionOptionsTest
{
    [TestMethod]
    public void DefaultsMakeSevenAttempts()
    {
        var options = new DeliveryReactionOptions();

        Assert.AreSame(DeliveryReactionOptions.DefaultRetryDelays, options.EffectiveRetryDelays);
        Assert.AreEqual(7, options.MaxAttempts);
    }

    [TestMethod]
    public void ConfiguredRetryDelaysReplaceTheDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Delivery:Reactions:RetryDelays:0"] = "00:02:00",
                ["Delivery:Reactions:RetryDelays:1"] = "01:00:00",
            })
            .Build();

        var options = configuration.GetSection(DeliveryReactionOptions.SectionName).Get<DeliveryReactionOptions>();

        Assert.IsNotNull(options);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromMinutes(2), TimeSpan.FromHours(1) }, options.EffectiveRetryDelays.ToArray());
        Assert.AreEqual(3, options.MaxAttempts);
    }

    [TestMethod]
    public void ValidateAcceptsTheDefaults()
    {
        Assert.IsEmpty(Validate(new DeliveryReactionOptions()));
    }

    [TestMethod]
    [DataRow("00:00:00")]
    [DataRow("-00:01:00")]
    public void ValidateRejectsTimeLimitThatIsNotPositive(string timeLimit)
    {
        var options = new DeliveryReactionOptions { TimeLimit = TimeSpan.Parse(timeLimit, CultureInfo.InvariantCulture) };

        var result = Assert.ContainsSingle(Validate(options));
        CollectionAssert.AreEqual(new[] { nameof(DeliveryReactionOptions.TimeLimit) }, result.MemberNames.ToArray());
    }

    [TestMethod]
    [DataRow("00:00:00")]
    [DataRow("-00:00:15")]
    public void ValidateRejectsPollIntervalThatIsNotPositive(string pollInterval)
    {
        var options = new DeliveryReactionOptions { PollInterval = TimeSpan.Parse(pollInterval, CultureInfo.InvariantCulture) };

        var result = Assert.ContainsSingle(Validate(options));
        CollectionAssert.AreEqual(new[] { nameof(DeliveryReactionOptions.PollInterval) }, result.MemberNames.ToArray());
    }

    [TestMethod]
    public void ValidateRejectsRetryDelayThatIsNotPositive()
    {
        var options = new DeliveryReactionOptions { RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.Zero] };

        var result = Assert.ContainsSingle(Validate(options));
        CollectionAssert.AreEqual(new[] { nameof(DeliveryReactionOptions.RetryDelays) }, result.MemberNames.ToArray());
    }

    private static List<ValidationResult> Validate(DeliveryReactionOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
