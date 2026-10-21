using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Geopilot.Pipeline.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.ComponentModel.DataAnnotations;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class ReactionSetValidationTest
{
    private const string FileDrop = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel";

    // A valid pipeline, so only the channels and the sets can make a definition invalid.
    private const string Pipelines = """
        processes:
          - id: zip
            implementation: Geopilot.Pipeline.Processes.ZipPackage.ZipPackageProcess
        pipelines:
          - id: zip
            display_name:
              en: Zip
            steps:
              - id: zip
                display_name:
                  en: Zip
                process_id: zip
                input:
                  input: "${upload()}"

        """;

    // A file drop without a default configuration, so a reaction or a mandate has to provide the subfolder.
    private const string FileDropChannel = $$"""
        channels:
          - id: ablage
            implementation: {{FileDrop}}

        """;

    private const string FileDropChannelWithSubfolder = $$"""
        channels:
          - id: ablage
            implementation: {{FileDrop}}
            default_config:
              subfolder: allgemein

        """;

    private const string PlainSet = """
        reaction_sets:
          - id: ablage
            transfers:
              - id: ablage
                channel_id: ablage
        """;

    private const string OverwritingSet = """
        reaction_sets:
          - id: ablage
            transfers:
              - id: ablage
                channel_id: ablage
                channel_config_overwrites:
                  subfolder: kanton-a
        """;

    private const string CollisionOrigin =
        "Channel configuration collision for implementation '" + FileDrop + "': " +
        "the key 'subfolder' is set in the base configuration " +
        "(app settings 'Pipeline:ChannelConfigs:" + FileDrop + ":subfolder', " +
        "environment variable 'Pipeline__ChannelConfigs__" + FileDrop + "__subfolder')";

    private const string CollisionRemedy =
        " The base configuration cannot be overridden. Remove the key from the pipeline definition, " +
        "or remove it from the base configuration if the value has to be set per reaction or per mandate.";

    private static readonly Dictionary<string, Parameterization> BaseConfigsWithSubfolder = new()
    {
        [FileDrop] = new Parameterization { ["baseDirectory"] = "/drop", ["subfolder"] = "kanton-a" },
    };

    private PipelineProcessFactory processFactory;
    private Mock<ILoggerFactory> loggerFactoryMock;

    [TestInitialize]
    public void SetUp()
    {
        var pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        pipelineOptionsMock.SetupGet(o => o.Value).Returns(new PipelineOptions { Definition = "pipeline.yaml", Plugins = [] });

        var ilitoolsOptionsMock = new Mock<IOptions<IlitoolsOptions>>();
        ilitoolsOptionsMock.SetupGet(o => o.Value).Returns(new IlitoolsOptions { IlitoolsWrapperAddress = "http://localhost:5555" });

        loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);

        processFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, ilitoolsOptionsMock.Object, loggerFactoryMock.Object);
    }

    [TestCleanup]
    public void Cleanup() => processFactory?.Dispose();

    [TestMethod]
    public void AcceptsADefinitionWithoutSets()
    {
        var result = Factory(string.Empty).ValidateDefinition();

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    public void AcceptsAFileDropSet()
    {
        var result = Factory(FileDropChannel + FileDropSet("ablage", "ablage")).ValidateDefinition();

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    public void RejectsTwoChannelsWithTheSameId()
    {
        var channels = FileDropChannel + $$"""
              - id: ablage
                implementation: {{FileDrop}}

            """;

        AssertInvalid(Factory(channels + FileDropSet("weitergabe", "weitergabe")), "PipelineProcessConfig: Duplicate Id found: ablage.");
    }

    [TestMethod]
    public void RejectsAChannelWithoutImplementation()
    {
        var channels = """
            channels:
              - id: ablage

            """;

        AssertInvalid(Factory(channels + FileDropSet("ablage", "ablage")), "ChannelConfig (Implementation): Channel Implementation is required.");
    }

    [TestMethod]
    public void RejectsAReactionWithoutChannel()
    {
        var sets = """
            reaction_sets:
              - id: ablage
                transfers:
                  - id: ablage
            """;

        AssertInvalid(Factory(FileDropChannel + sets), "ReactionConfig (ChannelId): Channel Reference is required.");
    }

    [TestMethod]
    public void RejectsAReferenceToAnUndefinedChannel()
    {
        AssertInvalid(
            Factory(FileDropChannel + FileDropSet("ablage", "ablage", channelId: "archiv")),
            "PipelineProcessConfig: One or more reactions reference a channel that is not defined in the channels collection: archiv.");
    }

    [TestMethod]
    public void RejectsTwoSetsWithTheSameId()
    {
        var sets = FileDropSet("ablage", "ablage_a") + FileDropSet("ablage", "ablage_b", withHeader: false);

        AssertInvalid(Factory(FileDropChannel + sets), "PipelineProcessConfig: Duplicate Id found: ablage.");
    }

    [TestMethod]
    public void RejectsAReactionIdUsedInTwoSets()
    {
        var sets = FileDropSet("ablage", "ablage") + FileDropSet("ablage_zwei", "ablage", withHeader: false);

        AssertInvalid(Factory(FileDropChannel + sets), "PipelineProcessConfig: The reaction id 'ablage' is used more than once. Reaction ids must be unique across all reaction sets.");
    }

    [TestMethod]
    public void RejectsASetWithoutTransfers()
    {
        var sets = """
            reaction_sets:
              - id: ablage
                transfers: []
            """;

        AssertInvalid(Factory(sets), "ReactionSetConfig (Transfers): At least one Transfer is required.");
    }

    [TestMethod]
    [DataRow("Ablage", DisplayName = "Uppercase letter")]
    [DataRow("_ablage", DisplayName = "Leading underscore")]
    [DataRow("ab lage", DisplayName = "Space")]
    public void RejectsAReactionIdThatIsNoPlainName(string reactionId)
    {
        AssertInvalid(Factory(FileDropChannel + FileDropSet("ablage", reactionId)), "ReactionConfig (Id): A reaction id consists of lowercase letters, digits, '_' and '-', starts with a letter or digit and has at most 128 characters.");
    }

    [TestMethod]
    public void RejectsAMandateKeyDeclaredTwice()
    {
        var sets = """
            reaction_sets:
              - id: ablage
                transfers:
                  - id: ablage
                    channel_id: ablage
                    mandate_config:
                      - key: subfolder
                        display_name:
                          de: Unterordner
                      - key: subfolder
                        display_name:
                          de: Noch ein Unterordner
            """;

        AssertInvalid(Factory(FileDropChannel + sets), "ReactionConfig: Duplicate Key found: subfolder.");
    }

    [TestMethod]
    public void RejectsAMandateKeyWithoutDisplayName()
    {
        var sets = """
            reaction_sets:
              - id: ablage
                transfers:
                  - id: ablage
                    channel_id: ablage
                    mandate_config:
                      - key: subfolder
            """;

        AssertInvalid(Factory(FileDropChannel + sets), "MandateConfigEntry (DisplayName): Mandate Config Display Name is required.");
    }

    [TestMethod]
    public void RejectsAnUnknownChannel()
    {
        var channels = FileDropChannel.Replace(FileDrop, "Geopilot.Pipeline.Channels.Missing", StringComparison.Ordinal);

        AssertInvalid(Factory(channels + FileDropSet("ablage", "ablage")), "reaction set ablage, reaction ablage, channel ablage, error: Channel <Geopilot.Pipeline.Channels.Missing> is unknown.");
    }

    [TestMethod]
    public void RejectsAMandateKeyTheChannelDoesNotMark()
    {
        AssertInvalid(
            Factory(FileDropChannel + FileDropSet("ablage", "ablage", mandateKey: "baseDirectory")),
            $"reaction set ablage, reaction ablage, channel ablage, error: Channel <{FileDrop}>: the mandate value 'baseDirectory' is not a constructor parameter marked [MandateValue].");
    }

    [TestMethod]
    public void RequiresTheBaseValuesWithABaseConfiguration()
    {
        var factory = Factory(FileDropChannel + FileDropSet("ablage", "ablage"), new Dictionary<string, Parameterization>());

        AssertInvalid(factory, "reaction set ablage, reaction ablage, channel ablage, error: Channel initialization: No suitable parameter found for parameter of type <String> and name <baseDirectory>.");
    }

    [TestMethod]
    public void AcceptsTheBaseValuesWithABaseConfiguration()
    {
        var factory = Factory(FileDropChannel + FileDropSet("ablage", "ablage"), new Dictionary<string, Parameterization>
        {
            [FileDrop] = new Parameterization { ["baseDirectory"] = "/drop" },
        });

        var result = factory.ValidateDefinition();

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    public void AcceptsASubfolderTheChannelSets()
    {
        var result = Factory(FileDropChannelWithSubfolder + PlainSet).ValidateDefinition();

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    public void AcceptsASubfolderTheReactionOverwrites()
    {
        var result = Factory(FileDropChannelWithSubfolder + OverwritingSet).ValidateDefinition();

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    public void RejectsAnOverwriteOfAKeyTheDefaultConfigLacks()
    {
        AssertInvalid(
            Factory(FileDropChannel + OverwritingSet),
            "PipelineProcessConfig: Reaction 'ablage' in reaction set 'ablage' is trying to overwrite channel config parameter 'subfolder' which is not defined in the default config.");
    }

    [TestMethod]
    public void RejectsAMandateValueTheDefaultConfigSets()
    {
        AssertInvalid(
            Factory(FileDropChannelWithSubfolder + FileDropSet("ablage", "ablage")),
            $"reaction set ablage, reaction ablage, channel ablage, error: Channel <{FileDrop}>: the mandate value 'subfolder' is already set in default_config. A value comes either from the pipeline definition or from the mandate.");
    }

    [TestMethod]
    public void RejectsADefaultConfigKeyTheBaseConfigurationSets()
    {
        // No reaction uses the channel, and the collision still fails the start, as it does for a process.
        AssertInvalid(
            Factory(FileDropChannelWithSubfolder, BaseConfigsWithSubfolder),
            "PipelineProcessConfig: " + CollisionOrigin + " and in the pipeline definition ('channels[id=ablage].default_config.subfolder')." + CollisionRemedy);
    }

    [TestMethod]
    public void RejectsAnOverwriteTheBaseConfigurationSets()
    {
        AssertInvalid(
            Factory(FileDropChannelWithSubfolder + OverwritingSet, BaseConfigsWithSubfolder),
            CollisionOrigin + " and overwritten in the pipeline definition ('reaction_sets[id=ablage].transfers[id=ablage].channel_config_overwrites.subfolder')." + CollisionRemedy);
    }

    [TestMethod]
    public void RejectsAMandateValueTheBaseConfigurationSets()
    {
        AssertInvalid(
            Factory(FileDropChannel + FileDropSet("ablage", "ablage"), BaseConfigsWithSubfolder),
            CollisionOrigin + " and declared as a mandate value in the pipeline definition ('reaction_sets[id=ablage].transfers[id=ablage].mandate_config[key=subfolder]')." + CollisionRemedy);
    }

    [TestMethod]
    public void UniqueReactionIdsAcceptsNoSets()
    {
        Assert.IsNull(new UniqueReactionIdsAttribute().GetValidationResult(null, new ValidationContext(new object())));
    }

    [TestMethod]
    public void UniqueReactionIdsRejectsAValueOfAnotherType()
    {
        var result = new UniqueReactionIdsAttribute().GetValidationResult("no sets", new ValidationContext(new object()));

        Assert.AreEqual("validation object is not of type IEnumerable<ReactionSetConfig>", result?.ErrorMessage);
    }

    [TestMethod]
    public void ResolvesATransferWithTheDefaultConfiguration()
    {
        var transfer = Assert.ContainsSingle(Factory(FileDropChannelWithSubfolder + PlainSet).ResolveTransfers("ablage"));

        Assert.AreEqual("ablage", transfer.ReactionId);
        Assert.AreEqual(FileDrop, transfer.Implementation);
        Assert.AreEqual("allgemein", transfer.Config["subfolder"]);
    }

    [TestMethod]
    public void ResolvesATransferWithTheOverwrittenConfiguration()
    {
        var transfer = Assert.ContainsSingle(Factory(FileDropChannelWithSubfolder + OverwritingSet).ResolveTransfers("ablage"));

        Assert.AreEqual("kanton-a", transfer.Config["subfolder"]);
    }

    [TestMethod]
    public void RefusesToResolveAnUnknownSet()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => Factory(FileDropChannelWithSubfolder + PlainSet).ResolveTransfers("unbekannt"));

        Assert.AreEqual("Reaction set <unbekannt> does not exist.", exception.Message);
    }

    private static void AssertInvalid(PipelineFactory factory, string expectedError)
    {
        var result = factory.ValidateDefinition();

        Assert.IsFalse(result.IsValid);
        Assert.Contains(expectedError, result.ErrorMessage);
    }

    private static string FileDropSet(string setId, string reactionId, string channelId = "ablage", string mandateKey = "subfolder", bool withHeader = true)
        => (withHeader ? "reaction_sets:\n" : string.Empty) + $$"""
              - id: {{setId}}
                transfers:
                  - id: "{{reactionId}}"
                    channel_id: {{channelId}}
                    mandate_config:
                      - key: {{mandateKey}}
                        display_name:
                          de: Unterordner

            """;

    private PipelineFactory Factory(string definition, IReadOnlyDictionary<string, Parameterization>? channelBaseConfigs = null)
        => PipelineFactory
            .Builder()
            .Yaml(Pipelines + definition)
            .PipelineProcessFactory(processFactory)
            .LoggerFactory(loggerFactoryMock.Object)
            .PipelineTempDirectory(Path.Combine(Path.GetTempPath(), "Pipeline"))
            .ChannelBaseConfigs(channelBaseConfigs)
            .Build();
}
