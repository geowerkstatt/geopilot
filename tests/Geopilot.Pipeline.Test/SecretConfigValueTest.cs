using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace Geopilot.Pipeline.Test;

/// <summary>
/// A process parameter marked [SecretConfigValue] takes its value from the base configuration only and never reaches
/// the definition snapshot of a run in clear text.
/// </summary>
[TestClass]
public class SecretConfigValueTest
{
    private const string TokenProcess = "Geopilot.Pipeline.Test.Processes.SecretTokenProcess";
    private const string NumberProcess = "Geopilot.Pipeline.Test.Processes.SecretNumberProcess";

    [TestMethod]
    public void ValidateDefinitionRejectsSecretInDefaultConfigOfUnusedProcess()
    {
        var yaml = $"""
            processes:
              - id: used
                implementation: {TokenProcess}
                default_config:
                  host: smtp.example.com
              - id: unused
                implementation: {TokenProcess}
                default_config:
                  token: written-into-the-definition
            {Pipeline("used")}
            """;
        using var processFactory = CreateProcessFactory();

        var result = CreateFactory(yaml, processFactory).ValidateDefinition();

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.ErrorMessage, "[SecretConfigValue]");
        StringAssert.Contains(result.ErrorMessage, "processes[id=unused].default_config.token");
    }

    [TestMethod]
    public void ValidateDefinitionRejectsSecretInStepOverwrite()
    {
        var yaml = $"""
            processes:
              - id: used
                implementation: {TokenProcess}
                default_config:
                  token: written-into-the-definition
            pipelines:
              - id: secret_pipeline
                display_name:
                  en: Secret pipeline
                steps:
                  - id: only
                    display_name:
                      en: Only step
                    process_id: used
                    process_config_overwrites:
                      token: overwritten-in-the-step
            """;
        using var processFactory = CreateProcessFactory();

        var result = CreateFactory(yaml, processFactory).ValidateDefinition();

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.ErrorMessage, "pipelines[id=secret_pipeline].steps[id=only].process_config_overwrites.token");
    }

    [TestMethod]
    public void ValidateDefinitionRejectsSecretOnNonStringParameter()
    {
        var yaml = $"""
            processes:
              - id: used
                implementation: {TokenProcess}
              - id: number
                implementation: {NumberProcess}
            {Pipeline("used")}
            """;
        using var processFactory = CreateProcessFactory();

        var result = CreateFactory(yaml, processFactory).ValidateDefinition();

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.ErrorMessage, "[SecretConfigValue]");
        StringAssert.Contains(result.ErrorMessage, "'pin'");
    }

    [TestMethod]
    public void ValidateDefinitionAcceptsSecretFromBaseConfig()
    {
        var yaml = $"""
            processes:
              - id: used
                implementation: {TokenProcess}
            {Pipeline("used")}
            """;
        using var processFactory = CreateProcessFactory();
        var baseConfigs = new Dictionary<string, Parameterization> { [TokenProcess] = new Parameterization { { "token", "from-the-base" } } };

        var result = CreateFactory(yaml, processFactory, baseConfigs).ValidateDefinition();

        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void GetDefinitionSnapshotJsonMasksSecretAndDropsUnknownKey()
    {
        var secret = $"secret-{Guid.NewGuid()}";
        var typo = $"typo-{Guid.NewGuid()}";
        var yaml = $"""
            processes:
              - id: used
                implementation: {TokenProcess}
            {Pipeline("used")}
            """;
        using var processFactory = CreateProcessFactory();
        var baseConfigs = new Dictionary<string, Parameterization>
        {
            [TokenProcess] = new Parameterization { { "host", "smtp.example.com" }, { "token", secret }, { "tokn", typo } },
        };

        var json = CreateFactory(yaml, processFactory, baseConfigs).GetDefinitionSnapshotJson("secret_pipeline");

        Assert.IsFalse(json.Contains(secret, StringComparison.Ordinal), json);
        Assert.IsFalse(json.Contains(typo, StringComparison.Ordinal), json);
        using var document = JsonDocument.Parse(json);
        var config = document.RootElement.GetProperty("process_configs").GetProperty(TokenProcess);
        Assert.AreEqual("smtp.example.com", config.GetProperty("host").GetString());
        Assert.AreEqual("**********", config.GetProperty("token").GetString());
        Assert.IsFalse(config.TryGetProperty("tokn", out _), json);
    }

    [TestMethod]
    public void GetDefinitionSnapshotJsonLeavesUnsetSecretUnmasked()
    {
        var yaml = $"""
            processes:
              - id: used
                implementation: {TokenProcess}
            {Pipeline("used")}
            """;
        using var processFactory = CreateProcessFactory();
        var baseConfigs = new Dictionary<string, Parameterization>
        {
            [TokenProcess] = new Parameterization { { "host", "smtp.example.com" }, { "token", null } },
        };

        var json = CreateFactory(yaml, processFactory, baseConfigs).GetDefinitionSnapshotJson("secret_pipeline");

        Assert.IsFalse(json.Contains("**********", StringComparison.Ordinal), json);
    }

    [TestMethod]
    public void GetDefinitionSnapshotJsonLeavesEmptySecretUnmasked()
    {
        // The options binder hands an empty setting, such as an unset environment variable in compose, over as "".
        var yaml = $"""
            processes:
              - id: used
                implementation: {TokenProcess}
            {Pipeline("used")}
            """;
        using var processFactory = CreateProcessFactory();
        var baseConfigs = new Dictionary<string, Parameterization>
        {
            [TokenProcess] = new Parameterization { { "host", "smtp.example.com" }, { "token", string.Empty } },
        };

        var json = CreateFactory(yaml, processFactory, baseConfigs).GetDefinitionSnapshotJson("secret_pipeline");

        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(string.Empty, document.RootElement.GetProperty("process_configs").GetProperty(TokenProcess).GetProperty("token").GetString(), json);
    }

    [TestMethod]
    public void GetDefinitionSnapshotJsonDropsBaseConfigOfUnresolvableImplementation()
    {
        const string unknownProcess = "Geopilot.Pipeline.Test.Processes.UnknownProcess";
        var yaml = $"""
            processes:
              - id: used
                implementation: {unknownProcess}
            {Pipeline("used")}
            """;
        using var processFactory = CreateProcessFactory();
        var baseConfigs = new Dictionary<string, Parameterization>
        {
            [unknownProcess] = new Parameterization { { "token", "never-in-the-snapshot" } },
        };

        var json = CreateFactory(yaml, processFactory, baseConfigs).GetDefinitionSnapshotJson("secret_pipeline");

        Assert.IsFalse(json.Contains("never-in-the-snapshot", StringComparison.Ordinal), json);
    }

    // One pipeline with one step that runs the given process.
    private static string Pipeline(string processId) => $"""
        pipelines:
          - id: secret_pipeline
            display_name:
              en: Secret pipeline
            steps:
              - id: only
                display_name:
                  en: Only step
                process_id: {processId}
        """;

    // Loads this test assembly as a processor plugin, because GetProcessorType resolves only the built-in processes
    // from the app domain.
    private static PipelineProcessFactory CreateProcessFactory()
    {
        var options = new Mock<IOptions<PipelineOptions>>();
        options.SetupGet(o => o.Value).Returns(new PipelineOptions
        {
            Definition = "",
            Plugins = [typeof(SecretConfigValueTest).Assembly.Location],
            ProcessConfigs = new Dictionary<string, Parameterization>(),
        });
        var ilitoolsOptions = new Mock<IOptions<IlitoolsOptions>>();
        ilitoolsOptions.SetupGet(o => o.Value).Returns(new IlitoolsOptions { IlitoolsWrapperAddress = "http://localhost:5555" });
        return new PipelineProcessFactory(options.Object, ilitoolsOptions.Object, NullLoggerFactory.Instance);
    }

    private static PipelineFactory CreateFactory(string yaml, IPipelineProcessFactory processFactory, IReadOnlyDictionary<string, Parameterization>? processBaseConfigs = null)
        => PipelineFactory
            .Builder()
            .Yaml(yaml)
            .PipelineProcessFactory(processFactory)
            .LoggerFactory(NullLoggerFactory.Instance)
            .PipelineTempDirectory(Path.Combine(Path.GetTempPath(), "Pipeline"))
            .ProcessBaseConfigs(processBaseConfigs)
            .Build();
}
