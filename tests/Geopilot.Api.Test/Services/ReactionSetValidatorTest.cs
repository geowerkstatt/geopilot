using Geopilot.Pipeline;
using Geopilot.Pipeline.Config;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace Geopilot.Api.Services;

[TestClass]
public class ReactionSetValidatorTest
{
    private const string SetId = "ablage_pro_mandat";
    private const string ReactionId = "weitergabe";

    private static readonly ReactionSetConfig Set = new()
    {
        Id = SetId,
        DisplayName = new Dictionary<string, string> { ["de"] = "Ablage pro Mandat" },
        Transfers =
        [
            new ReactionConfig
            {
                Id = ReactionId,
                ChannelId = "ablage",
                MandateConfig = [new MandateConfigEntry { Key = "subfolder", DisplayName = new Dictionary<string, string> { ["de"] = "Unterordner" } }],
            },
        ],
    };

    private Mock<IPipelineFactory> pipelineFactoryMock;
    private DeliveryOptions deliveryOptions;

    [TestInitialize]
    public void Initialize()
    {
        pipelineFactoryMock = new Mock<IPipelineFactory>();
        pipelineFactoryMock.SetupGet(factory => factory.ReactionSets).Returns([Set]);
        pipelineFactoryMock
            .Setup(factory => factory.ValidateMandateValues(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, Parameterization>>()))
            .Returns([]);

        // Without uploader deletion the window closes with the declaration.
        deliveryOptions = new DeliveryOptions { UploaderDeleteEnabled = false };
    }

    [TestMethod]
    public void CheckAcceptsNoSetAndDropsAllValues()
    {
        var check = Validator().Check(null, Values(ReactionId, "subfolder", "kanton-a"));

        Assert.IsTrue(check.IsValid);
        Assert.IsEmpty(check.Values);
        pipelineFactoryMock.Verify(factory => factory.ValidateMandateValues(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, Parameterization>>()), Times.Never);
    }

    [TestMethod]
    public void CheckRejectsAnUnknownSet()
    {
        var check = Validator().Check("unbekannt", Values(ReactionId, "subfolder", "kanton-a"));

        Assert.IsFalse(check.IsValid);
        Assert.AreEqual("Reaction set <unbekannt> does not exist.", check.SetError);
    }

    [TestMethod]
    public void CheckRejectsASetWhileTheDeleteWindowNeverCloses()
    {
        deliveryOptions = new DeliveryOptions { UploaderDeleteEnabled = true };

        var check = Validator().Check(SetId, Values(ReactionId, "subfolder", "kanton-a"));

        Assert.IsFalse(check.IsValid);
        Assert.AreEqual(
            "A reaction set needs a delete window that closes, because its reactions only run once the uploader can no longer delete the delivery. Set Delivery:DeleteDuration or Delivery:DeleteRestrictInterval.",
            check.SetError);
    }

    [TestMethod]
    public void CheckKeepsOnlyTheValuesTheSetDeclares()
    {
        IReadOnlyDictionary<string, Parameterization>? checkedValues = null;
        pipelineFactoryMock
            .Setup(factory => factory.ValidateMandateValues(SetId, It.IsAny<IReadOnlyDictionary<string, Parameterization>>()))
            .Callback<string, IReadOnlyDictionary<string, Parameterization>>((_, values) => checkedValues = values)
            .Returns([]);
        var values = Values(ReactionId, "subfolder", "kanton-a");
        values[ReactionId]["unknown"] = JsonSerializer.SerializeToElement("not declared");
        values["other_reaction"] = new Dictionary<string, JsonElement> { ["subfolder"] = JsonSerializer.SerializeToElement("kanton-b") };

        var check = Validator().Check(SetId, values);

        Assert.IsTrue(check.IsValid);
        Assert.AreEqual(Json(Values(ReactionId, "subfolder", "kanton-a")), Json(check.Values));
        Assert.IsNotNull(checkedValues);
        var checkedReaction = Assert.ContainsSingle(checkedValues);
        Assert.AreEqual(ReactionId, checkedReaction.Key);
        CollectionAssert.AreEqual(new[] { "subfolder" }, checkedReaction.Value.Keys.ToArray());
    }

    [TestMethod]
    public void CheckReportsWhatTheChannelsRefuse()
    {
        pipelineFactoryMock
            .Setup(factory => factory.ValidateMandateValues(SetId, It.IsAny<IReadOnlyDictionary<string, Parameterization>>()))
            .Returns([$"reaction {ReactionId}: The subfolder is not valid."]);

        var check = Validator().Check(SetId, Values(ReactionId, "subfolder", "../anderswo"));

        Assert.IsFalse(check.IsValid);
        Assert.IsNull(check.SetError);
        CollectionAssert.AreEqual(new[] { $"reaction {ReactionId}: The subfolder is not valid." }, check.ValueErrors.ToArray());
    }

    private static Dictionary<string, Dictionary<string, JsonElement>> Values(string reactionId, string key, string text)
        => new() { [reactionId] = new Dictionary<string, JsonElement> { [key] = JsonSerializer.SerializeToElement(text) } };

    private static string Json(object value) => JsonSerializer.Serialize(value);

    private ReactionSetValidator Validator() => new(pipelineFactoryMock.Object, Options.Create(deliveryOptions));
}
