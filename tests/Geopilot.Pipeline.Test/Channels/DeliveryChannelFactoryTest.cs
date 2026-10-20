using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Config;

namespace Geopilot.Pipeline.Test.Channels;

[TestClass]
public class DeliveryChannelFactoryTest
{
    private const string TargetChannel = "Geopilot.Pipeline.Test.Channels.TargetChannel";

    private static readonly Dictionary<string, Parameterization> HostConfigs = new()
    {
        [TargetChannel] = new Parameterization { ["target"] = "/drop" },
    };

    [TestMethod]
    public void ValidateAcceptsAChannelWhoseValuesAreAllProvided()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        Assert.IsEmpty(factory.Validate(TargetChannel, ["folder"]));
    }

    [TestMethod]
    public void ValidateLeavesHostValuesUncheckedWithoutAHostLayer()
    {
        var factory = new DeliveryChannelFactory(null);

        Assert.IsEmpty(factory.Validate(TargetChannel, ["folder"]));
    }

    [TestMethod]
    public void ValidateRejectsAnUnknownImplementation()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var error = Assert.ContainsSingle(factory.Validate("Geopilot.Pipeline.Test.Channels.Missing", []));
        Assert.AreEqual("Channel <Geopilot.Pipeline.Test.Channels.Missing> is unknown.", error);
    }

    [TestMethod]
    public void ValidateRejectsATypeThatIsNoChannel()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var error = Assert.ContainsSingle(factory.Validate("Geopilot.Pipeline.Test.Channels.NoChannel", []));
        Assert.AreEqual("<Geopilot.Pipeline.Test.Channels.NoChannel> is not a delivery channel.", error);
    }

    [TestMethod]
    public void ValidateRejectsAChannelWithTwoConstructors()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var error = Assert.ContainsSingle(factory.Validate("Geopilot.Pipeline.Test.Channels.TwoConstructorsChannel", []));
        Assert.AreEqual("Channel <Geopilot.Pipeline.Test.Channels.TwoConstructorsChannel> has 2 public constructors. A Channel must have exactly one public constructor.", error);
    }

    [TestMethod]
    [DataRow("target", DisplayName = "Host value")]
    [DataRow("unknown", DisplayName = "No parameter at all")]
    public void ValidateRejectsAMandateKeyTheChannelDoesNotMark(string key)
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, ["folder", key]));
        Assert.AreEqual($"Channel <{TargetChannel}>: the mandate value '{key}' is not a constructor parameter marked [MandateValue].", error);
    }

    [TestMethod]
    public void ValidateRejectsARequiredMandateValueTheSetDoesNotDeclare()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, []));
        Assert.AreEqual($"Channel <{TargetChannel}>: the parameter 'folder' takes a mandate value, so the set has to declare it in mandate_config.", error);
    }

    [TestMethod]
    public void ValidateRejectsAMissingHostValue()
    {
        var factory = new DeliveryChannelFactory(new Dictionary<string, Parameterization>());

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, ["folder"]));
        Assert.AreEqual("Channel initialization: No suitable parameter found for parameter of type <String> and name <target>. Parameter is not nullable, cannot initialize channel.", error);
    }

    [TestMethod]
    public void ValidateRejectsAHostValueThatDoesNotConvert()
    {
        var factory = new DeliveryChannelFactory(new Dictionary<string, Parameterization>
        {
            [TargetChannel] = new Parameterization { ["target"] = "/drop", ["timeoutSeconds"] = "soon" },
        });

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, ["folder"]));
        Assert.AreEqual("Channel initialization: The configured value <\"soon\"> for parameter <timeoutSeconds> cannot be converted to type <Int32?>.", error);
    }

    [TestMethod]
    public void ValidateRejectsAHostValueForAMandateValue()
    {
        var factory = new DeliveryChannelFactory(new Dictionary<string, Parameterization>
        {
            [TargetChannel] = new Parameterization { ["target"] = "/drop", ["folder"] = "everyone" },
        });

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, ["folder"]));
        Assert.AreEqual($"Channel <{TargetChannel}>: the parameter 'folder' takes a mandate value and cannot be set in Pipeline:ChannelConfigs.", error);
    }

    [TestMethod]
    public void CreateBindsHostAndMandateValues()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var channel = factory.Create(TargetChannel, new Parameterization
        {
            ["folder"] = "mandate-a",
            ["recipients"] = new List<object> { "a@example.com", "b@example.com" },
        });

        var target = Assert.IsInstanceOfType<TargetChannel>(channel);
        Assert.AreEqual("/drop", target.Target);
        Assert.AreEqual("mandate-a", target.Folder);
        CollectionAssert.AreEqual(new[] { "a@example.com", "b@example.com" }, target.Recipients?.ToArray());
        Assert.IsNull(target.TimeoutSeconds);
    }

    [TestMethod]
    public void CreateTakesNothingButMandateValuesFromTheMandate()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var channel = factory.Create(TargetChannel, new Parameterization { ["folder"] = "mandate-a", ["target"] = "/elsewhere" });

        Assert.AreEqual("/drop", Assert.IsInstanceOfType<TargetChannel>(channel).Target);
    }

    [TestMethod]
    public void CreateRefusesAMissingRequiredValue()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => factory.Create(TargetChannel, new Parameterization()));
        Assert.AreEqual("Channel initialization: No suitable parameter found for parameter of type <String> and name <folder>. Parameter is not nullable, cannot initialize channel.", exception.Message);
    }
}

internal sealed class TargetChannel : IDeliveryChannel
{
    public TargetChannel(string target, [MandateValue] string folder, [MandateValue] IReadOnlyList<string>? recipients, int? timeoutSeconds)
    {
        Target = target;
        Folder = folder;
        Recipients = recipients;
        TimeoutSeconds = timeoutSeconds;
    }

    public string Target { get; }

    public string Folder { get; }

    public IReadOnlyList<string>? Recipients { get; }

    public int? TimeoutSeconds { get; }

    public Task<ChannelResult> DeliverAsync(DeliveryInfo delivery, IReadOnlyList<IDeliveryFile> files, CancellationToken cancellationToken)
        => Task.FromResult(ChannelResult.Success("Delivered."));
}

internal sealed class TwoConstructorsChannel : IDeliveryChannel
{
    public TwoConstructorsChannel()
    {
    }

    public TwoConstructorsChannel(string target)
    {
        _ = target;
    }

    public Task<ChannelResult> DeliverAsync(DeliveryInfo delivery, IReadOnlyList<IDeliveryFile> files, CancellationToken cancellationToken)
        => Task.FromResult(ChannelResult.Success("Delivered."));
}

internal sealed class NoChannel
{
}
