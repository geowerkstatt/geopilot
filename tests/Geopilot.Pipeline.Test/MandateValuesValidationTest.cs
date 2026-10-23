using Geopilot.Pipeline.Channels;
using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class MandateValuesValidationTest
{
    private const string FileDrop = "Geopilot.Pipeline.Channels.FileDrop.FileDropChannel";
    private const string SetId = "ablage_pro_mandat";
    private const string ReactionId = "weitergabe";

    private const string Definition = $$"""
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
        channels:
          - id: ablage
            implementation: {{FileDrop}}
        reaction_sets:
          - id: {{SetId}}
            display_name:
              de: Ablage pro Mandat
            transfers:
              - id: {{ReactionId}}
                channel_id: ablage
                mandate_config:
                  - key: subfolder
                    display_name:
                      de: Unterordner
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
    public void ListsTheSetsOfTheDefinition()
    {
        var set = Assert.ContainsSingle(Factory().ReactionSets);

        Assert.AreEqual(SetId, set.Id);
        Assert.AreEqual("Ablage pro Mandat", set.DisplayName["de"]);
    }

    [TestMethod]
    public void DescribesTheValuesTheSetAsksTheMandateFor()
    {
        var field = Assert.ContainsSingle(Factory().DescribeMandateFields(SetId));

        Assert.AreEqual(ReactionId, field.ReactionId);
        Assert.AreEqual("subfolder", field.Key);
        Assert.AreEqual("Unterordner", field.DisplayName["de"]);
        Assert.AreEqual(MandateValueType.Text, field.Type);
        Assert.IsTrue(field.Required);
    }

    [TestMethod]
    public void DescribingTheValuesOfAnUnknownSetFails()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => Factory().DescribeMandateFields("unbekannt"));

        Assert.AreEqual("Reaction set <unbekannt> does not exist.", exception.Message);
    }

    [TestMethod]
    public void AcceptsValuesTheChannelsTake()
    {
        Assert.IsEmpty(Factory().ValidateMandateValues(SetId, Values("\"kanton-a\"")));
    }

    [TestMethod]
    public void RejectsAMissingValue()
    {
        var error = Assert.ContainsSingle(Factory().ValidateMandateValues(SetId, new Dictionary<string, Parameterization>()));

        Assert.AreEqual($"reaction {ReactionId}: Channel initialization: No suitable parameter found for parameter of type <String> and name <subfolder>. Parameter is not nullable, cannot initialize channel.", error);
    }

    [TestMethod]
    public void RejectsAValueTheChannelRefuses()
    {
        var error = Assert.ContainsSingle(Factory().ValidateMandateValues(SetId, Values("\"../anderswo\"")));

        Assert.AreEqual($"reaction {ReactionId}: The subfolder <../anderswo> is not a relative path without '..'. (Parameter 'subfolder')", error);
    }

    [TestMethod]
    public void RejectsAListWhereTheChannelTakesAText()
    {
        var error = Assert.ContainsSingle(Factory().ValidateMandateValues(SetId, Values("[\"kanton-a\", \"kanton-b\"]")));

        StringAssert.StartsWith(error, $"reaction {ReactionId}: Channel initialization: The configured value");
        StringAssert.EndsWith(error, "for parameter <subfolder> cannot be converted to type <String>.");
    }

    [TestMethod]
    public void RefusesAnUnknownSet()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => Factory().ValidateMandateValues("unbekannt", Values("\"kanton-a\"")));

        Assert.AreEqual("Reaction set <unbekannt> does not exist.", exception.Message);
    }

    private static Dictionary<string, Parameterization> Values(string subfolderJson)
        => new() { [ReactionId] = new Parameterization { ["subfolder"] = JsonSerializer.Deserialize<JsonElement>(subfolderJson) } };

    private PipelineFactory Factory()
        => PipelineFactory
            .Builder()
            .Yaml(Definition)
            .PipelineProcessFactory(processFactory)
            .LoggerFactory(loggerFactoryMock.Object)
            .PipelineTempDirectory(Path.Combine(Path.GetTempPath(), "Pipeline"))
            .ChannelBaseConfigs(new Dictionary<string, Parameterization>
            {
                [FileDrop] = new Parameterization { ["baseDirectory"] = Path.Combine(Path.GetTempPath(), "FileDrop") },
            })
            .Build();
}
