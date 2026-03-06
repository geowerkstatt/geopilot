using Geopilot.Api.Pipeline;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;

namespace Geopilot.Api.Test.Pipeline;

[TestClass]
public class PipelineProcessFactoryTest
{
    private Mock<IOptions<PipelineOptions>> pipelineOptionsMock;
    private Mock<ILogger<PipelineProcessFactory>> loggerMock;
    private Mock<ILoggerFactory> loggerFactoryMock;
    private PipelineProcessFactory pipelineProcessFactory;

    [TestInitialize]
    public void Initialize()
    {
        pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        loggerMock = new Mock<ILogger<PipelineProcessFactory>>();
        loggerFactoryMock = new Mock<ILoggerFactory>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        pipelineProcessFactory?.Dispose();
    }

    [TestMethod]
    public void CreateProcessMergesConfigs()
    {
        // Arrange: Set up three levels of configuration hierarchy
        // Base config (lowest priority) - defined in PipelineOptions.ProcessConfigs
        var baseConfig = new Parameterization()
        {
            { "checkServiceBaseUrl", "http://base.test/" }, // Required parameter for XtfValidatorProcess
        };

        // Default config (medium priority) - defined in ProcessConfig.DefaultConfig
        var defaultConfig = new Parameterization()
        {
            { "pollInterval", "2000" }, // Can override (exists in default config)
            { "validationProfile", "DEFAULT_PROFILE" },  // defines a parameter only in default config
        };

        // Overwrites (highest priority) - can only override keys present in default config
        var overwrites = new Parameterization()
        {
            { "pollInterval", "1000" }, // Can override (exists in default config)
        };

        // Set up PipelineOptions with base configuration
        var pipelineOptions = new PipelineOptions()
        {
            Definition = "",
            ProcessConfigs = new Dictionary<string, Parameterization>()
            {
                { "Geopilot.Api.Pipeline.Process.XtfValidatorProcess", baseConfig },
            },
        };

        pipelineOptionsMock.SetupGet(o => o.Value).Returns(pipelineOptions);
        pipelineProcessFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, loggerMock.Object, loggerFactoryMock.Object);

        // Set up StepConfig and ProcessConfig
        var stepConfig = new StepConfig()
        {
            Id = "test_step",
            DisplayName = new Dictionary<string, string> { { "en", "Test Step" } },
            ProcessId = "test_process",
            ProcessConfigOverwrites = overwrites,
            Output = new List<OutputConfig>
            {
                new OutputConfig { Take = "result", As = "result" },
            },
        };

        var processConfig = new ProcessConfig()
        {
            Id = "test_process",
            Implementation = "Geopilot.Api.Pipeline.Process.XtfValidatorProcess",
            DefaultConfig = defaultConfig,
        };

        var processes = new List<ProcessConfig> { processConfig };

        // Act: Create process which triggers configuration merging
        var process = pipelineProcessFactory.CreateProcess(stepConfig, processes);

        // Assert: Verify the merged configuration
        Assert.IsNotNull(process, "Process should be created");
        Assert.IsInstanceOfType<XtfValidatorProcess>(process, "Process should be of type XtfValidatorProcess");

        // Use reflection to access the private config field
        var configuredHttpClient = typeof(XtfValidatorProcess)
            ?.GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(process) as HttpClient;
        Assert.AreEqual("http://base.test/", configuredHttpClient?.BaseAddress?.ToString(), "Check Service Base Url not as expected");

        var configuredValidationProfile = typeof(XtfValidatorProcess)
            ?.GetField("validationProfile", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(process) as string;
        Assert.AreEqual("DEFAULT_PROFILE", configuredValidationProfile, "Check Service validation profile not as expected");

        var configuredPollInterval = typeof(XtfValidatorProcess)
            ?.GetField("pollInterval", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(process);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1000.0), configuredPollInterval, "Check Service poll intervall not as expected");
    }
}
