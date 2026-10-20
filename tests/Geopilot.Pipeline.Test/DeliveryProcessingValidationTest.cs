using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class DeliveryProcessingValidationTest
{
    private const string FileDrop = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel";

    // A valid pipeline, so only the sets can make a definition invalid.
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
        var result = Factory(FileDropSet("ablage", "ablage")).ValidateDefinition();

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    public void RejectsTwoSetsWithTheSameId()
    {
        var sets = FileDropSet("ablage", "ablage_a") + FileDropSet("ablage", "ablage_b", withHeader: false);

        AssertInvalid(Factory(sets), "PipelineProcessConfig: Duplicate Id found: ablage.");
    }

    [TestMethod]
    public void RejectsAReactionIdUsedInTwoSets()
    {
        var sets = FileDropSet("ablage", "ablage") + FileDropSet("ablage_zwei", "ablage", withHeader: false);

        AssertInvalid(Factory(sets), "PipelineProcessConfig: The reaction id 'ablage' is used more than once. Reaction ids must be unique across all sets of delivery_processing.");
    }

    [TestMethod]
    public void RejectsASetWithoutTransfers()
    {
        var sets = """
            delivery_processing:
              - id: ablage
                transfers: []
            """;

        AssertInvalid(Factory(sets), "DeliveryProcessingConfig (Transfers): At least one Transfer is required.");
    }

    [TestMethod]
    [DataRow("Ablage", DisplayName = "Uppercase letter")]
    [DataRow("_ablage", DisplayName = "Leading underscore")]
    [DataRow("ab lage", DisplayName = "Space")]
    public void RejectsAReactionIdThatIsNoPlainName(string reactionId)
    {
        AssertInvalid(Factory(FileDropSet("ablage", reactionId)), "ReactionConfig (Id): A reaction id consists of lowercase letters, digits, '_' and '-', starts with a letter or digit and has at most 128 characters.");
    }

    [TestMethod]
    public void RejectsAMandateKeyDeclaredTwice()
    {
        var sets = $$"""
            delivery_processing:
              - id: ablage
                transfers:
                  - id: ablage
                    implementation: {{FileDrop}}
                    mandate_config:
                      - key: subfolder
                        display_name:
                          de: Unterordner
                      - key: subfolder
                        display_name:
                          de: Noch ein Unterordner
            """;

        AssertInvalid(Factory(sets), "ReactionConfig: Duplicate Key found: subfolder.");
    }

    [TestMethod]
    public void RejectsAMandateKeyWithoutDisplayName()
    {
        var sets = $$"""
            delivery_processing:
              - id: ablage
                transfers:
                  - id: ablage
                    implementation: {{FileDrop}}
                    mandate_config:
                      - key: subfolder
            """;

        AssertInvalid(Factory(sets), "MandateConfigEntry (DisplayName): Mandate Config Display Name is required.");
    }

    [TestMethod]
    public void RejectsAnUnknownChannel()
    {
        var sets = FileDropSet("ablage", "ablage").Replace(FileDrop, "Geopilot.Pipeline.Channels.Missing", StringComparison.Ordinal);

        AssertInvalid(Factory(sets), "delivery processing ablage, reaction ablage, error: Channel <Geopilot.Pipeline.Channels.Missing> is unknown.");
    }

    [TestMethod]
    public void RejectsAMandateKeyTheChannelDoesNotMark()
    {
        AssertInvalid(
            Factory(FileDropSet("ablage", "ablage", mandateKey: "baseDirectory")),
            $"delivery processing ablage, reaction ablage, error: Channel <{FileDrop}>: the mandate value 'baseDirectory' is not a constructor parameter marked [MandateValue].");
    }

    [TestMethod]
    public void RequiresTheHostValuesWithAHostLayer()
    {
        var factory = Factory(FileDropSet("ablage", "ablage"), new Dictionary<string, Parameterization>());

        AssertInvalid(factory, "delivery processing ablage, reaction ablage, error: Channel initialization: No suitable parameter found for parameter of type <String> and name <baseDirectory>.");
    }

    [TestMethod]
    public void AcceptsTheHostValuesWithAHostLayer()
    {
        var factory = Factory(FileDropSet("ablage", "ablage"), new Dictionary<string, Parameterization>
        {
            [FileDrop] = new Parameterization { ["baseDirectory"] = "/drop" },
        });

        var result = factory.ValidateDefinition();

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    public void AcceptsASubfolderTheSetFixes()
    {
        var result = Factory(FixedSubfolderSet()).ValidateDefinition();

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    public void RejectsAKeyTheSetFixesAndTheMandateSupplies()
    {
        var sets = $$"""
            delivery_processing:
              - id: ablage
                transfers:
                  - id: ablage
                    implementation: {{FileDrop}}
                    config:
                      subfolder: kanton-a
                    mandate_config:
                      - key: subfolder
                        display_name:
                          de: Unterordner
            """;

        AssertInvalid(Factory(sets), $"delivery processing ablage, reaction ablage, error: Channel <{FileDrop}>: the key 'subfolder' is set in config and mandate_config.");
    }

    [TestMethod]
    public void RejectsAKeyTheSetFixesAndTheHostSets()
    {
        var sets = FixedSubfolderSet().Replace("subfolder: kanton-a", "baseDirectory: /elsewhere", StringComparison.Ordinal);
        var factory = Factory(sets, new Dictionary<string, Parameterization>
        {
            [FileDrop] = new Parameterization { ["baseDirectory"] = "/drop", ["subfolder"] = "kanton-a" },
        });

        AssertInvalid(factory, $"delivery processing ablage, reaction ablage, error: Channel <{FileDrop}>: the key 'baseDirectory' is set in Pipeline:ChannelConfigs and config.");
    }

    private static string FixedSubfolderSet() => $$"""
        delivery_processing:
          - id: ablage
            transfers:
              - id: ablage
                implementation: {{FileDrop}}
                config:
                  subfolder: kanton-a
        """;

    private static void AssertInvalid(PipelineFactory factory, string expectedError)
    {
        var result = factory.ValidateDefinition();

        Assert.IsFalse(result.IsValid);
        Assert.Contains(expectedError, result.ErrorMessage);
    }

    private static string FileDropSet(string setId, string reactionId, string mandateKey = "subfolder", bool withHeader = true)
        => (withHeader ? "delivery_processing:\n" : string.Empty) + $$"""
              - id: {{setId}}
                transfers:
                  - id: "{{reactionId}}"
                    implementation: {{FileDrop}}
                    mandate_config:
                      - key: {{mandateKey}}
                        display_name:
                          de: Unterordner

            """;

    private PipelineFactory Factory(string sets, IReadOnlyDictionary<string, Parameterization>? channelBaseConfigs = null)
        => PipelineFactory
            .Builder()
            .Yaml(Pipelines + sets)
            .PipelineProcessFactory(processFactory)
            .LoggerFactory(loggerFactoryMock.Object)
            .PipelineTempDirectory(Path.Combine(Path.GetTempPath(), "Pipeline"))
            .ChannelBaseConfigs(channelBaseConfigs)
            .Build();
}
