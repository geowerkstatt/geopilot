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
    private PipelineProcessFactory pipelineProcessFactory;

    [TestInitialize]
    public void Initialize()
    {
        pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        loggerMock = new Mock<ILogger<PipelineProcessFactory>>();
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
            { "InterlisCheckServiceUrl", "http://base.test/" }, // Required parameter for XtfValidatorProcess
            { "BaseOnly", "base_value" },           // Should remain unchanged (only in base)
            { "BaseAndDefault", "base_value" },     // Should be overwritten by default config
            { "BaseDefaultAndOverwrite", "base_value" }, // Should be overwritten by default, then by overwrite
            { "BaseAndOverwrite", "base_value" },   // Overwrite attempts to change this, but cannot (not in default config)
        };

        // Default config (medium priority) - defined in ProcessConfig.DefaultConfig
        var defaultConfig = new Parameterization()
        {
            { "BaseAndDefault", "default_value" },  // Overwrites base config
            { "BaseDefaultAndOverwrite", "default_value" }, // Overwrites base, will be overwritten again
            { "DefaultOnly", "default_only_value" }, // Only in default config
        };

        // Overwrites (highest priority) - can only override keys present in default config
        var overwrites = new Parameterization()
        {
            { "BaseDefaultAndOverwrite", "overwrite_value" }, // Can override (exists in default config)
            { "BaseAndOverwrite", "overwrite_value" },        // Cannot override (not in default config) - should log warning
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
        pipelineProcessFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, loggerMock.Object);

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
        var configField = typeof(XtfValidatorProcess).GetField("config", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(configField, "Config field should exist");

        var mergedConfig = configField.GetValue(process) as Dictionary<string, string>;
        Assert.IsNotNull(mergedConfig, "Merged config should not be null");

        // Verify configuration hierarchy is correctly applied
        Assert.HasCount(6, mergedConfig, "Merged config should contain 6 parameters");

        // 0. Required parameter from base config (not overridden)
        Assert.IsTrue(mergedConfig.ContainsKey("InterlisCheckServiceUrl"), "InterlisCheckServiceUrl should be present");
        Assert.AreEqual("http://base.test/", mergedConfig["InterlisCheckServiceUrl"], "InterlisCheckServiceUrl should have base value");

        // 1. Value from base config only (not overridden)
        Assert.IsTrue(mergedConfig.ContainsKey("BaseOnly"), "BaseOnly should be present");
        Assert.AreEqual("base_value", mergedConfig["BaseOnly"], "BaseOnly should have base value");

        // 2. Value overridden by default config
        Assert.IsTrue(mergedConfig.ContainsKey("BaseAndDefault"), "BaseAndDefault should be present");
        Assert.AreEqual("default_value", mergedConfig["BaseAndDefault"], "BaseAndDefault should have default value");

        // 3. Value overridden by both default and overwrite config (highest priority wins)
        Assert.IsTrue(mergedConfig.ContainsKey("BaseDefaultAndOverwrite"), "BaseDefaultAndOverwrite should be present");
        Assert.AreEqual("overwrite_value", mergedConfig["BaseDefaultAndOverwrite"], "BaseDefaultAndOverwrite should have overwrite value");

        // 4. Value from default config only
        Assert.IsTrue(mergedConfig.ContainsKey("DefaultOnly"), "DefaultOnly should be present");
        Assert.AreEqual("default_only_value", mergedConfig["DefaultOnly"], "DefaultOnly should have default only value");

        // 5. Value from base config - overwrite attempt ignored (not in default config)
        Assert.IsTrue(mergedConfig.ContainsKey("BaseAndOverwrite"), "BaseAndOverwrite should be present");
        Assert.AreEqual("base_value", mergedConfig["BaseAndOverwrite"], "BaseAndOverwrite should retain base value (overwrite ignored)");

        // Verify that warning was logged for invalid overwrite attempt
        loggerMock.VerifyMessageContains(LogLevel.Warning, "BaseAndOverwrite", "overwrite_value");
    }
}
