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

        Assert.IsEmpty(factory.Validate(TargetChannel, null, ["folder"]));
    }

    [TestMethod]
    public void ValidateAcceptsAMandateValueTheSetFixes()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        Assert.IsEmpty(factory.Validate(TargetChannel, new Parameterization { ["folder"] = "fixed" }, []));
    }

    [TestMethod]
    public void ValidateAcceptsAHostValueTheSetProvidesInstead()
    {
        var factory = new DeliveryChannelFactory(new Dictionary<string, Parameterization>());

        Assert.IsEmpty(factory.Validate(TargetChannel, new Parameterization { ["target"] = "/drop" }, ["folder"]));
    }

    [TestMethod]
    public void ValidateLeavesMissingValuesUncheckedWithoutAHostLayer()
    {
        var factory = new DeliveryChannelFactory(null);

        Assert.IsEmpty(factory.Validate(TargetChannel, null, []));
    }

    [TestMethod]
    public void ValidateRejectsAnUnknownImplementation()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var error = Assert.ContainsSingle(factory.Validate("Geopilot.Pipeline.Test.Channels.Missing", null, []));
        Assert.AreEqual("Channel <Geopilot.Pipeline.Test.Channels.Missing> is unknown.", error);
    }

    [TestMethod]
    public void ValidateRejectsATypeThatIsNoChannel()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var error = Assert.ContainsSingle(factory.Validate("Geopilot.Pipeline.Test.Channels.NoChannel", null, []));
        Assert.AreEqual("<Geopilot.Pipeline.Test.Channels.NoChannel> is not a delivery channel.", error);
    }

    [TestMethod]
    public void ValidateRejectsAChannelWithTwoConstructors()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var error = Assert.ContainsSingle(factory.Validate("Geopilot.Pipeline.Test.Channels.TwoConstructorsChannel", null, []));
        Assert.AreEqual("Channel <Geopilot.Pipeline.Test.Channels.TwoConstructorsChannel> has 2 public constructors. A Channel must have exactly one public constructor.", error);
    }

    [TestMethod]
    [DataRow("timeoutSeconds", DisplayName = "Host value")]
    [DataRow("unknown", DisplayName = "No parameter at all")]
    public void ValidateRejectsAMandateKeyTheChannelDoesNotMark(string key)
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, null, ["folder", key]));
        Assert.AreEqual($"Channel <{TargetChannel}>: the mandate value '{key}' is not a constructor parameter marked [MandateValue].", error);
    }

    [TestMethod]
    public void ValidateRejectsAConfigKeyNoParameterTakes()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, new Parameterization { ["subfoldr"] = "typo" }, ["folder"]));
        Assert.AreEqual($"Channel <{TargetChannel}>: the config key 'subfoldr' is not a constructor parameter.", error);
    }

    [TestMethod]
    public void ValidateRejectsAConfigValueThatDoesNotConvert()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, new Parameterization { ["timeoutSeconds"] = "soon" }, ["folder"]));
        Assert.AreEqual("Channel initialization: The configured value <\"soon\"> for parameter <timeoutSeconds> cannot be converted to type <Int32?>.", error);
    }

    [TestMethod]
    public void ValidateRejectsARequiredMandateValueNobodyProvides()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, null, []));
        Assert.AreEqual($"Channel <{TargetChannel}>: the parameter 'folder' has no value. Set it in config or declare it in mandate_config.", error);
    }

    [TestMethod]
    public void ValidateRejectsAMissingHostValue()
    {
        var factory = new DeliveryChannelFactory(new Dictionary<string, Parameterization>());

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, null, ["folder"]));
        Assert.AreEqual("Channel initialization: No suitable parameter found for parameter of type <String> and name <target>. Parameter is not nullable, cannot initialize channel.", error);
    }

    [TestMethod]
    public void ValidateRejectsAHostValueThatDoesNotConvert()
    {
        var factory = new DeliveryChannelFactory(new Dictionary<string, Parameterization>
        {
            [TargetChannel] = new Parameterization { ["target"] = "/drop", ["timeoutSeconds"] = "soon" },
        });

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, null, ["folder"]));
        Assert.AreEqual("Channel initialization: The configured value <\"soon\"> for parameter <timeoutSeconds> cannot be converted to type <Int32?>.", error);
    }

    [TestMethod]
    [DataRow(true, true, false, "Pipeline:ChannelConfigs and config", DisplayName = "Host and set")]
    [DataRow(true, false, true, "Pipeline:ChannelConfigs and mandate_config", DisplayName = "Host and mandate")]
    [DataRow(false, true, true, "config and mandate_config", DisplayName = "Set and mandate")]
    public void ValidateRejectsAKeySetInTwoPlaces(bool host, bool set, bool mandate, string places)
    {
        var hostValues = new Parameterization { ["target"] = "/drop" };
        if (host)
            hostValues["folder"] = "host";

        var factory = new DeliveryChannelFactory(new Dictionary<string, Parameterization> { [TargetChannel] = hostValues });
        var config = set ? new Parameterization { ["folder"] = "set" } : null;

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, config, mandate ? ["folder"] : []));
        Assert.AreEqual($"Channel <{TargetChannel}>: the key 'folder' is set in {places}. A key belongs to exactly one of Pipeline:ChannelConfigs, config and mandate_config.", error);
    }

    [TestMethod]
    public void CreateBindsHostAndMandateValues()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var channel = factory.Create(TargetChannel, null, new Parameterization
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
    public void CreateBindsTheValuesOfTheSet()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var channel = factory.Create(TargetChannel, new Parameterization { ["folder"] = "fixed", ["timeoutSeconds"] = "30" }, null);

        var target = Assert.IsInstanceOfType<TargetChannel>(channel);
        Assert.AreEqual("fixed", target.Folder);
        Assert.AreEqual(30, target.TimeoutSeconds);
    }

    [TestMethod]
    public void CreateTakesNothingButMandateValuesFromTheMandate()
    {
        var factory = new DeliveryChannelFactory(new Dictionary<string, Parameterization>());

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            factory.Create(TargetChannel, null, new Parameterization { ["folder"] = "mandate-a", ["target"] = "/elsewhere" }));
        Assert.Contains("name <target>", exception.Message);
    }

    [TestMethod]
    public void CreatePrefersTheHostOverTheSet()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var channel = factory.Create(TargetChannel, new Parameterization { ["folder"] = "fixed", ["target"] = "/elsewhere" }, null);

        Assert.AreEqual("/drop", Assert.IsInstanceOfType<TargetChannel>(channel).Target);
    }

    [TestMethod]
    public void CreateRefusesAMissingRequiredValue()
    {
        var factory = new DeliveryChannelFactory(HostConfigs);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => factory.Create(TargetChannel, null, null));
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
