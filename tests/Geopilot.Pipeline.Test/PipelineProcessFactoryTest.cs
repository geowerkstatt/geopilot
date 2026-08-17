using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Ilitools;
using Geopilot.Pipeline.Process;
using Geopilot.Pipeline.Processes.XtfValidation;
using Geopilot.Pipeline.Test.Processes;
using Geopilot.PipelineCore.Ilitools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;

namespace Geopilot.Pipeline.Test;

[TestClass]
public class PipelineProcessFactoryTest
{
    private Mock<ILogger<PipelineProcessFactory>> loggerMock;
    private Mock<ILoggerFactory> loggerFactoryMock;
    private Mock<IOptions<IlitoolsOptions>> ilitoolsOptionsMock;

    [TestInitialize]
    public void Initialize()
    {
        loggerMock = new Mock<ILogger<PipelineProcessFactory>>();
        loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(lf => lf.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);
        ilitoolsOptionsMock = new Mock<IOptions<IlitoolsOptions>>();
        ilitoolsOptionsMock.SetupGet(o => o.Value).Returns(new IlitoolsOptions { IlitoolsWrapperAddress = "http://localhost:5555" });
    }

    [TestMethod(DisplayName = "Create Process With List Config")]
    public void CreateProcessWithListConfig()
    {
    }

    [TestMethod(DisplayName = "Create Process Merges Configs")]
    public void CreateProcessMergesConfigs()
    {
        // Arrange: Set up three levels of configuration hierarchy
        // Base config (lowest priority) - defined in PipelineOptions.ProcessConfigs
        var baseConfig = new Parameterization()
        {
            { "modelDirs", "https://base.test/" }, // defines a parameter only in base config
        };

        // Default config (medium priority) - defined in ProcessConfig.DefaultConfig
        var defaultConfig = new Parameterization()
        {
            { "validationProfile", "DEFAULT_PROFILE" }, // Can override (exists in default config)
        };

        // Overwrites (highest priority) - can only override keys present in default config
        var overwrites = new Parameterization()
        {
            { "validationProfile", "PROFILE-A" }, // Can override (exists in default config)
        };

        Assembly executingAssembly = Assembly.GetExecutingAssembly();
        var path = Path.GetDirectoryName(executingAssembly.Location);
        var testDllFullPath = Path.Combine(path, "Geopilot.Pipeline.Test.dll");

        // Set up PipelineOptions with base configuration
        var pipelineOptions = new PipelineOptions()
        {
            Definition = "",
            Plugins = testDllFullPath != null ? [testDllFullPath] : [],
            ProcessConfigs = new Dictionary<string, Parameterization>()
            {
                { "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess", baseConfig },
            },
        };
        var pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        pipelineOptionsMock.SetupGet(o => o.Value).Returns(pipelineOptions);
        using var pipelineProcessFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, ilitoolsOptionsMock.Object, loggerFactoryMock.Object);

        // Set up StepConfig and ProcessConfig
        var stepConfig = new StepConfig()
        {
            Id = "test_step",
            DisplayName = new Dictionary<string, string> { { "en", "Test Step" } },
            ProcessId = "test_process",
            ProcessConfigOverwrites = overwrites,
        };

        var processConfig = new ProcessConfig()
        {
            Id = "test_process",
            Implementation = "Geopilot.Pipeline.Processes.XtfValidation.XtfValidatorProcess",
            DefaultConfig = defaultConfig,
        };

        var processes = new List<ProcessConfig> { processConfig };

        // Act: Create process which triggers configuration merging
        var process = pipelineProcessFactory
            .Builder()
            .StepConfig(stepConfig)
            .Processes(processes)
            .PipelineDirectory(Path.GetTempPath())
            .JobId(Guid.NewGuid())
            .Build();

        // Assert: Verify the merged configuration
        Assert.IsNotNull(process, "Process should be created");
        Assert.IsInstanceOfType<XtfValidatorProcess>(process, "Process should be of type XtfValidatorProcess");

        // Use reflection to access the private config field
        var configuredArgs = typeof(XtfValidatorProcess)
            ?.GetField("validatorArgs", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(process) as IlivalidatorArgs;
        Assert.IsNotNull(configuredArgs, "Validator arguments not built from the merged configuration");
        Assert.AreEqual("ilidata:PROFILE-A", configuredArgs.MetaConfig, "Overwritten validation profile not as expected");
        Assert.AreEqual("https://base.test/", configuredArgs.ModelDirs?.Single(), "Model directory from the base config not as expected");
    }

    public abstract class InitialzationDataSourceAttribute : Attribute
    {
        public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
        {
            if (data != null && data.Length == 5)
            {
                var testName = data[0] as string ?? "unknown";
                var baseConfig = data[1] as Parameterization ?? new Parameterization();
                var defaultConfig = data[2] as Parameterization ?? new Parameterization();
                var overwrites = data[3] as Parameterization ?? new Parameterization();
                var configDisplayNames = new List<string>() { ToDisplayString("BASE CONFIG", baseConfig), ToDisplayString("DEFAULT CONFIG", defaultConfig), ToDisplayString("OVERWRITES", overwrites) }
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();
                return $"{testName} - {string.Join(" | ", configDisplayNames)}";
            }
            else
            {
                return null;
            }
        }

        private static string ToDisplayString(string title, Parameterization parameterization)
        {
            if (parameterization.Count > 0)
                return title + ": " + string.Join(", ", parameterization.Select(kv => $"{kv.Key}={kv.Value}"));
            else
                return string.Empty;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ManyDifferentInitialzationDataSourceSunnyDayAttribute : InitialzationDataSourceAttribute, ITestDataSource
    {
        public IEnumerable<object?[]> GetData(MethodInfo methodInfo)
        {
            yield return [
                "base config with mandatory fields",
                new Parameterization()
                {
                    { "mandatoryString", "mandatory string value" },
                    { "mandatoryInt", "123" },
                    { "mandatoryDouble", "123.456" },
                    { "mandatoryBoolean", "true" },
                },
                new Parameterization() { },
                new Parameterization() { },
                new ManyDifferentInitialzationAttributesTestProcess("mandatory string value", null, 123, null, 123.456, null, true, null, Mock.Of<IIli2GpkgClient>(), Mock.Of<IIlivalidatorClient>(), Mock.Of<ILogger<ManyDifferentInitialzationAttributesTestProcess>>())
            ];
            yield return [
                "default config with mandatory fields",
                new Parameterization() { },
                new Parameterization()
                {
                    { "mandatoryString", "mandatory string value" },
                    { "mandatoryInt", "123" },
                    { "mandatoryDouble", "123.456" },
                    { "mandatoryBoolean", "true" },
                },
                new Parameterization() { },
                new ManyDifferentInitialzationAttributesTestProcess("mandatory string value", null, 123, null, 123.456, null, true, null, Mock.Of<IIli2GpkgClient>(), Mock.Of<IIlivalidatorClient>(), Mock.Of<ILogger<ManyDifferentInitialzationAttributesTestProcess>>()),
            ];
            yield return [
                "default config with mandatory fields overwritten in overwrite config",
                new Parameterization() { },
                new Parameterization()
                {
                    { "mandatoryString", "mandatory string value" },
                    { "mandatoryInt", "123" },
                    { "mandatoryDouble", "123.456" },
                    { "mandatoryBoolean", "true" },
                },
                new Parameterization()
                {
                    { "mandatoryString", "overwritten mandatory string value" },
                    { "mandatoryInt", "456" },
                    { "mandatoryDouble", "456.789" },
                    { "mandatoryBoolean", "false" },
                },
                new ManyDifferentInitialzationAttributesTestProcess("overwritten mandatory string value", null, 456, null, 456.789, null, false, null, Mock.Of<IIli2GpkgClient>(), Mock.Of<IIlivalidatorClient>(), Mock.Of<ILogger<ManyDifferentInitialzationAttributesTestProcess>>()),
            ];
            yield return [
                "default config with all fields",
                new Parameterization() { },
                new Parameterization()
                {
                    { "mandatoryString", "mandatory string value" },
                    { "optionalString", "optional string value" },
                    { "mandatoryInt", "123" },
                    { "optionalInt", "234" },
                    { "mandatoryDouble", "345.678" },
                    { "optionalDouble", "456.789" },
                    { "mandatoryBoolean", "true" },
                    { "optionalBoolean", "true" },
                },
                new Parameterization() { },
                new ManyDifferentInitialzationAttributesTestProcess("mandatory string value", "optional string value", 123, 234, 345.678, 456.789, true, true, Mock.Of<IIli2GpkgClient>(), Mock.Of<IIlivalidatorClient>(), Mock.Of<ILogger<ManyDifferentInitialzationAttributesTestProcess>>()),
            ];
        }
    }

    [TestMethod(DisplayName = "Test Many Process Initialization Params, sunny day")]
    [ManyDifferentInitialzationDataSourceSunnyDay]
    public void TestManyProcessInitializationParams(
        string testName,
        Parameterization baseConfig,
        Parameterization defaultConfig,
        Parameterization overwrites,
        ManyDifferentInitialzationAttributesTestProcess expected)
    {
        Assembly executingAssembly = Assembly.GetExecutingAssembly();
        var path = Path.GetDirectoryName(executingAssembly.Location);
        var testDllFullPath = Path.Combine(path, "Geopilot.Pipeline.Test.dll");

        // Set up PipelineOptions with base configuration
        var pipelineOptions = new PipelineOptions()
        {
            Definition = "",
            Plugins = testDllFullPath != null ? [testDllFullPath] : [],
            ProcessConfigs = new Dictionary<string, Parameterization>()
            {
                { "Geopilot.Pipeline.Test.Processes.ManyDifferentInitialzationAttributesTestProcess", baseConfig },
            },
        };
        var pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        pipelineOptionsMock.SetupGet(o => o.Value).Returns(pipelineOptions);
        using var pipelineProcessFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, ilitoolsOptionsMock.Object, loggerFactoryMock.Object);

        // Set up StepConfig and ProcessConfig
        var stepConfig = new StepConfig()
        {
            Id = "test_step",
            DisplayName = new Dictionary<string, string> { { "en", "Test Step" } },
            ProcessId = "test_process",
            ProcessConfigOverwrites = overwrites,
        };

        var processConfig = new ProcessConfig()
        {
            Id = "test_process",
            Implementation = "Geopilot.Pipeline.Test.Processes.ManyDifferentInitialzationAttributesTestProcess",
            DefaultConfig = defaultConfig,
        };

        var processes = new List<ProcessConfig> { processConfig };

        // Act: Create process which triggers configuration merging
        var process = pipelineProcessFactory
            .Builder()
            .StepConfig(stepConfig)
            .Processes(processes)
            .PipelineDirectory(Path.GetTempPath())
            .JobId(Guid.NewGuid())
            .Build();

        // Assert: Verify the merged configuration
        Assert.IsNotNull(process, "Process should be created");

        var logger = process.GetType().GetProperty("Logger")?.GetValue(process);
        var ili2GpkgClient = process.GetType().GetProperty("Ili2GpkgClient")?.GetValue(process);
        var ilivalidatorClient = process.GetType().GetProperty("IlivalidatorClient")?.GetValue(process);
        var mandatoryString = process.GetType().GetProperty("MandatoryString")?.GetValue(process);
        var optionalString = process.GetType().GetProperty("OptionalString")?.GetValue(process);
        var mandatoryInt = process.GetType().GetProperty("MandatoryInt")?.GetValue(process);
        var optionalInt = process.GetType().GetProperty("OptionalInt")?.GetValue(process);
        var mandatoryDouble = process.GetType().GetProperty("MandatoryDouble")?.GetValue(process);
        var optionalDouble = process.GetType().GetProperty("OptionalDouble")?.GetValue(process);
        var mandatoryBoolean = process.GetType().GetProperty("MandatoryBoolean")?.GetValue(process);
        var optionalBoolean = process.GetType().GetProperty("OptionalBoolean")?.GetValue(process);

        Assert.IsNotNull(logger, "Logger not defined");
        Assert.IsInstanceOfType<IIli2GpkgClient>(ili2GpkgClient, "Ili2GpkgClient not injected");
        Assert.IsInstanceOfType<IIlivalidatorClient>(ilivalidatorClient, "IlivalidatorClient not injected");
        Assert.AreEqual(expected.MandatoryString, mandatoryString, "Mandatory String not as expected");
        Assert.AreEqual(expected.OptionalString, optionalString, "Optional String not as expected");
        Assert.AreEqual(expected.MandatoryInt, mandatoryInt, "Mandatory Int not as expected");
        Assert.AreEqual(expected.OptionalInt, optionalInt, "Optional Int not as expected");
        Assert.AreEqual(expected.MandatoryDouble, mandatoryDouble, "Mandatory Double not as expected");
        Assert.AreEqual(expected.OptionalDouble, optionalDouble, "Optional Double not as expected");
        Assert.AreEqual(expected.MandatoryBoolean, mandatoryBoolean, "Mandatory Boolean not as expected");
        Assert.AreEqual(expected.OptionalBoolean, optionalBoolean, "Optional Boolean not as expected");
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ManyDifferentInitialzationDataSourceExceptionAttribute : InitialzationDataSourceAttribute, ITestDataSource
    {
        public IEnumerable<object?[]> GetData(MethodInfo methodInfo)
        {
            yield return [
                "base config owerwritten in default config",
                new Parameterization()
                {
                    { "mandatoryString", "mandatory string value" },
                    { "mandatoryInt", "123" },
                    { "mandatoryDouble", "123.456" },
                    { "mandatoryBoolean", "true" },
                },
                new Parameterization()
                {
                    { "mandatoryString", "overwritten mandatory string value" },
                },
                new Parameterization() { },
                "Conflict in process configuration: The key 'mandatoryString' is defined in both process base configuration and process default configuration. Please resolve this conflict by ensuring that base configuration can't be overwritten."
            ];
            yield return [
                "base config owerwritten in overwrite config",
                new Parameterization()
                {
                    { "mandatoryString", "mandatory string value" },
                    { "mandatoryInt", "123" },
                    { "mandatoryDouble", "123.456" },
                    { "mandatoryBoolean", "true" },
                },
                new Parameterization() { },
                new Parameterization()
                {
                    { "mandatoryString", "overwritten mandatory string value" },
                },
                "Conflict in process configuration overwrite: The key 'mandatoryString' is defined in both process base configuration and process overwrite configuration. Please resolve this conflict by ensuring that base configuration can't be overwritten."
            ];
            yield return [
                "overwrite config but not defined in base config",
                new Parameterization() { },
                new Parameterization()
                {
                    { "mandatoryInt", "123" },
                    { "mandatoryDouble", "123.456" },
                    { "mandatoryBoolean", "true" },
                },
                new Parameterization()
                {
                    { "mandatoryString", "overwritten mandatory string value" },
                },
                "Conflict in process configuration overwrite: The key 'mandatoryString' is not defined in process default configuration, so it cannot be overwritten. Please ensure that only existing default configuration keys are overwritten."
            ];
            yield return [
                "don't initialize mandatory configuration",
                new Parameterization()
                {
                    { "mandatoryString", "mandatory string value" },
                    { "mandatoryInt", "123" },
                    { "mandatoryDouble", "123.456" },
                },
                new Parameterization() { },
                new Parameterization() { },
                "Process initialization: No suitable parameter found for parameter of type <Boolean> and name <mandatoryBoolean>. Parameter is not nullable, cannot initialize process."
            ];
            yield return [
                "configured value not convertible on nullable parameter",
                new Parameterization() { },
                new Parameterization()
                {
                    { "mandatoryString", "mandatory string value" },
                    { "mandatoryInt", "123" },
                    { "mandatoryDouble", "123.456" },
                    { "mandatoryBoolean", "true" },
                    { "optionalInt", "abc" },
                },
                new Parameterization() { },
                "Process initialization: The configured value <\"abc\"> for parameter <optionalInt> cannot be converted to type <Int32?>."
            ];
        }
    }

    [TestMethod(DisplayName = "Test Many Process Initialization Params, Overwrite not allowed")]
    [ManyDifferentInitialzationDataSourceException]
    public void TestManyProcessInitializationParamsException(
        string testName,
        Parameterization baseConfig,
        Parameterization defaultConfig,
        Parameterization overwrites,
        string expectedExceptionMessage)
    {
        Assembly executingAssembly = Assembly.GetExecutingAssembly();
        var path = Path.GetDirectoryName(executingAssembly.Location);
        var testDllFullPath = Path.Combine(path, "Geopilot.Pipeline.Test.dll");

        // Set up PipelineOptions with base configuration
        var pipelineOptions = new PipelineOptions()
        {
            Definition = "",
            Plugins = testDllFullPath != null ? [testDllFullPath] : [],
            ProcessConfigs = new Dictionary<string, Parameterization>()
            {
                { "Geopilot.Pipeline.Test.Processes.ManyDifferentInitialzationAttributesTestProcess", baseConfig },
            },
        };
        var pipelineOptionsMock = new Mock<IOptions<PipelineOptions>>();
        pipelineOptionsMock.SetupGet(o => o.Value).Returns(pipelineOptions);
        using var pipelineProcessFactory = new PipelineProcessFactory(pipelineOptionsMock.Object, ilitoolsOptionsMock.Object, loggerFactoryMock.Object);

        // Set up StepConfig and ProcessConfig
        var stepConfig = new StepConfig()
        {
            Id = "test_step",
            DisplayName = new Dictionary<string, string> { { "en", "Test Step" } },
            ProcessId = "test_process",
            ProcessConfigOverwrites = overwrites,
        };

        var processConfig = new ProcessConfig()
        {
            Id = "test_process",
            Implementation = "Geopilot.Pipeline.Test.Processes.ManyDifferentInitialzationAttributesTestProcess",
            DefaultConfig = defaultConfig,
        };

        var processes = new List<ProcessConfig> { processConfig };

        var exception = Assert.Throws<InvalidOperationException>(() => pipelineProcessFactory
            .Builder()
            .StepConfig(stepConfig)
            .Processes(processes)
            .PipelineDirectory(Path.GetTempPath())
            .JobId(Guid.NewGuid())
            .Build());
        Assert.AreEqual(expectedExceptionMessage, exception.Message, "Exception Message not as expected");
    }
}
