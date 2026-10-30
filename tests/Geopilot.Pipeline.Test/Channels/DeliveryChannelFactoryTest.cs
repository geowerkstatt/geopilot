using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Config;
using System.Text.Json;

namespace Geopilot.Pipeline.Test.Channels;

[TestClass]
public class DeliveryChannelFactoryTest
{
    private const string TargetChannel = "Geopilot.Pipeline.Test.Channels.TargetChannel";
    private const string SwitchChannel = "Geopilot.Pipeline.Test.Channels.SwitchChannel";

    private static readonly Dictionary<string, Parameterization> BaseConfigs = new()
    {
        [TargetChannel] = new Parameterization { ["target"] = "/drop" },
    };

    [TestMethod]
    public void ValidateAcceptsAChannelWhoseValuesAreAllProvided()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

        Assert.IsEmpty(factory.Validate(TargetChannel, null, ["folder"]));
    }

    [TestMethod]
    public void ValidateAcceptsAMandateValueTheDefinitionSets()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

        Assert.IsEmpty(factory.Validate(TargetChannel, new Parameterization { ["folder"] = "fixed" }, []));
    }

    [TestMethod]
    public void ValidateAcceptsABaseValueTheDefinitionProvidesInstead()
    {
        var factory = new DeliveryChannelFactory(new Dictionary<string, Parameterization>());

        Assert.IsEmpty(factory.Validate(TargetChannel, new Parameterization { ["target"] = "/drop" }, ["folder"]));
    }

    [TestMethod]
    public void ValidateLeavesMissingValuesUncheckedWithoutABaseConfiguration()
    {
        var factory = new DeliveryChannelFactory(null);

        Assert.IsEmpty(factory.Validate(TargetChannel, null, []));
    }

    [TestMethod]
    public void ValidateRejectsAnUnknownImplementation()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

        var error = Assert.ContainsSingle(factory.Validate("Geopilot.Pipeline.Test.Channels.Missing", null, []));
        Assert.AreEqual("Channel <Geopilot.Pipeline.Test.Channels.Missing> is unknown.", error);
    }

    [TestMethod]
    public void ValidateRejectsATypeThatIsNoChannel()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

        var error = Assert.ContainsSingle(factory.Validate("Geopilot.Pipeline.Test.Channels.NoChannel", null, []));
        Assert.AreEqual("<Geopilot.Pipeline.Test.Channels.NoChannel> is not a delivery channel.", error);
    }

    [TestMethod]
    public void ValidateRejectsAChannelWithTwoConstructors()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

        var error = Assert.ContainsSingle(factory.Validate("Geopilot.Pipeline.Test.Channels.TwoConstructorsChannel", null, []));
        Assert.AreEqual("Channel <Geopilot.Pipeline.Test.Channels.TwoConstructorsChannel> has 2 public constructors. A Channel must have exactly one public constructor.", error);
    }

    [TestMethod]
    [DataRow("timeoutSeconds", DisplayName = "Unmarked parameter")]
    [DataRow("unknown", DisplayName = "No parameter at all")]
    public void ValidateRejectsAMandateKeyTheChannelDoesNotMark(string key)
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, null, ["folder", key]));
        Assert.AreEqual($"Channel <{TargetChannel}>: the mandate value '{key}' is not a constructor parameter marked [MandateValue].", error);
    }

    [TestMethod]
    public void ValidateRejectsAConfigKeyNoParameterTakes()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, new Parameterization { ["subfoldr"] = "typo" }, ["folder"]));
        Assert.AreEqual($"Channel <{TargetChannel}>: the default_config key 'subfoldr' is not a constructor parameter.", error);
    }

    [TestMethod]
    public void ValidateRejectsAConfigValueThatDoesNotConvert()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, new Parameterization { ["timeoutSeconds"] = "soon" }, ["folder"]));
        Assert.AreEqual("Channel initialization: The configured value <\"soon\"> for parameter <timeoutSeconds> cannot be converted to type <Int32?>.", error);
    }

    [TestMethod]
    public void ValidateRejectsARequiredMandateValueNobodyProvides()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, null, []));
        Assert.AreEqual($"Channel <{TargetChannel}>: the parameter 'folder' has no value. Set it in default_config or declare it in mandate_config.", error);
    }

    [TestMethod]
    public void ValidateRejectsAMissingBaseValue()
    {
        var factory = new DeliveryChannelFactory(new Dictionary<string, Parameterization>());

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, null, ["folder"]));
        Assert.AreEqual("Channel initialization: No suitable parameter found for parameter of type <String> and name <target>. Parameter is not nullable, cannot initialize channel.", error);
    }

    [TestMethod]
    public void ValidateRejectsABaseValueThatDoesNotConvert()
    {
        var factory = new DeliveryChannelFactory(new Dictionary<string, Parameterization>
        {
            [TargetChannel] = new Parameterization { ["target"] = "/drop", ["timeoutSeconds"] = "soon" },
        });

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, null, ["folder"]));
        Assert.AreEqual("Channel initialization: The configured value <\"soon\"> for parameter <timeoutSeconds> cannot be converted to type <Int32?>.", error);
    }

    [TestMethod]
    public void ValidateRejectsAMandateValueOfAnotherType()
    {
        var factory = new DeliveryChannelFactory(null);

        var error = Assert.ContainsSingle(factory.Validate("Geopilot.Pipeline.Test.Channels.CountChannel", null, ["count"]));
        Assert.AreEqual("Channel <Geopilot.Pipeline.Test.Channels.CountChannel>: the parameter 'count' is marked [MandateValue] but takes Int32. A mandate value is a text (string), a list of texts (IReadOnlyList<string>) or a switch (bool).", error);
    }

    [TestMethod]
    public void ValidateAcceptsASwitchAsMandateValue()
    {
        var factory = new DeliveryChannelFactory(null);

        Assert.IsEmpty(factory.Validate(SwitchChannel, null, ["copyToDeclarer", "confirmed"]));
    }

    [TestMethod]
    public void ValidateRejectsAMandateValueTheDefinitionSets()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

        var error = Assert.ContainsSingle(factory.Validate(TargetChannel, new Parameterization { ["folder"] = "fixed" }, ["folder"]));
        Assert.AreEqual($"Channel <{TargetChannel}>: the mandate value 'folder' is already set in default_config. A value comes either from the pipeline definition or from the mandate.", error);
    }

    [TestMethod]
    public void ValidateRejectsABaseConfigValueTheDefinitionSets()
    {
        var factory = new DeliveryChannelFactory(null);

        var error = Assert.ContainsSingle(factory.Validate("Geopilot.Pipeline.Test.Channels.SecretChannel", new Parameterization { ["password"] = "in the definition" }, []));
        Assert.AreEqual("Channel <Geopilot.Pipeline.Test.Channels.SecretChannel>: the parameter 'password' is marked [BaseConfigValue] and can only be set in the base configuration (Pipeline:ChannelConfigs), not in the pipeline definition.", error);
    }

    [TestMethod]
    public void ValidateRejectsTheBaseDirectoryOfAFileDropInTheDefinition()
    {
        var factory = new DeliveryChannelFactory(null);

        var error = Assert.ContainsSingle(factory.Validate("Geopilot.Pipeline.Channels.FileDrop.FileDropChannel", new Parameterization { ["baseDirectory"] = "/drop", ["subfolder"] = "mandate-a" }, []));
        Assert.Contains("'baseDirectory' is marked [BaseConfigValue]", error);
    }

    [TestMethod]
    public void CreateBindsBaseAndMandateValues()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

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
    public void CreateBindsTheValuesOfTheDefinition()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

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
    public void CreatePrefersTheBaseConfigurationOverTheDefinition()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

        var channel = factory.Create(TargetChannel, new Parameterization { ["folder"] = "fixed", ["target"] = "/elsewhere" }, null);

        Assert.AreEqual("/drop", Assert.IsInstanceOfType<TargetChannel>(channel).Target);
    }

    [TestMethod]
    public void CreateRefusesAMissingRequiredValue()
    {
        var factory = new DeliveryChannelFactory(BaseConfigs);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => factory.Create(TargetChannel, null, null));
        Assert.AreEqual("Channel initialization: No suitable parameter found for parameter of type <String> and name <folder>. Parameter is not nullable, cannot initialize channel.", exception.Message);
    }

    [TestMethod]
    public void DescribeMandateValuesTellsATextFromAListAndARequiredValueFromAnOptionalOne()
    {
        var values = new DeliveryChannelFactory(null).DescribeMandateValues(TargetChannel);

        Assert.HasCount(2, values);
        Assert.AreEqual((MandateValueType.Text, true), values["folder"]);
        Assert.AreEqual((MandateValueType.TextList, false), values["recipients"]);
    }

    [TestMethod]
    public void DescribeMandateValuesTellsASwitch()
    {
        var values = new DeliveryChannelFactory(null).DescribeMandateValues(SwitchChannel);

        Assert.AreEqual((MandateValueType.Switch, false), values["copyToDeclarer"]);
        Assert.AreEqual((MandateValueType.Switch, true), values["confirmed"]);
    }

    [TestMethod]
    public void CreateBindsASwitchFromTheJsonOfTheMandate()
    {
        var factory = new DeliveryChannelFactory(null);

        var channel = factory.Create(SwitchChannel, null, new Parameterization
        {
            ["copyToDeclarer"] = JsonSerializer.SerializeToElement(true),
            ["confirmed"] = JsonSerializer.SerializeToElement(false),
        });

        var switches = Assert.IsInstanceOfType<SwitchChannel>(channel);
        Assert.IsTrue(switches.CopyToDeclarer);
        Assert.IsFalse(switches.Confirmed);
    }

    [TestMethod]
    public void CreateLeavesAnOptionalSwitchUnsetWithoutAValue()
    {
        var channel = new DeliveryChannelFactory(null).Create(SwitchChannel, null, new Parameterization { ["confirmed"] = JsonSerializer.SerializeToElement(true) });

        Assert.IsNull(Assert.IsInstanceOfType<SwitchChannel>(channel).CopyToDeclarer);
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

internal sealed class SecretChannel : IDeliveryChannel
{
    public SecretChannel([BaseConfigValue] string password)
    {
        _ = password;
    }

    public Task<ChannelResult> DeliverAsync(DeliveryInfo delivery, IReadOnlyList<IDeliveryFile> files, CancellationToken cancellationToken)
        => Task.FromResult(ChannelResult.Success("Delivered."));
}

internal sealed class SwitchChannel : IDeliveryChannel
{
    public SwitchChannel([MandateValue] bool? copyToDeclarer, [MandateValue] bool confirmed)
    {
        CopyToDeclarer = copyToDeclarer;
        Confirmed = confirmed;
    }

    public bool? CopyToDeclarer { get; }

    public bool Confirmed { get; }

    public Task<ChannelResult> DeliverAsync(DeliveryInfo delivery, IReadOnlyList<IDeliveryFile> files, CancellationToken cancellationToken)
        => Task.FromResult(ChannelResult.Success("Delivered."));
}

internal sealed class CountChannel : IDeliveryChannel
{
    public CountChannel([MandateValue] int count)
    {
        _ = count;
    }

    public Task<ChannelResult> DeliverAsync(DeliveryInfo delivery, IReadOnlyList<IDeliveryFile> files, CancellationToken cancellationToken)
        => Task.FromResult(ChannelResult.Success("Delivered."));
}
