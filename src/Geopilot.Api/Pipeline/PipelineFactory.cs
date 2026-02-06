using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using System.Reflection;
using System.Threading;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Factory for creating <see cref="Pipeline"/> instances from YAML configuration.
/// </summary>
internal class PipelineFactory
{
    private readonly ILogger<PipelineFactory> logger;

    public PipelineProcessConfig PipelineProcessConfig { get; }

    private IConfiguration? configuration;
    private CancellationToken? cancellationToken;

    private PipelineFactory(
        PipelineProcessConfig? pipelineProcessConfig,
        IConfiguration? configuration,
        CancellationToken? cancellationToken)
    {
        this.PipelineProcessConfig = pipelineProcessConfig ?? throw new InvalidOperationException("Missing pileline process congifuration.");
        this.configuration = configuration;
        this.cancellationToken = cancellationToken;

        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        this.logger = factory.CreateLogger<PipelineFactory>();
    }

    /// <summary>
    /// Creates a pipeline instance with the specified id.
    /// </summary>
    /// <param name="id">The id of the pipeline to be created. References to <see cref="PipelineConfig.Id"/>.</param>
    /// <returns>A <see cref="Pipeline"/> instance.</returns>
    /// <exception cref="Exception">Thrown when the pipeline cannot be created.</exception>
    internal Pipeline CreatePipeline(string id)
    {
        var pipelineConfig = PipelineProcessConfig.Pipelines.Find(p => p.Id == id);

        if (pipelineConfig != null)
        {
            return new Pipeline(pipelineConfig.Id, pipelineConfig.DisplayName, CreateSteps(pipelineConfig), pipelineConfig.Parameters);
        }
        else
        {
            throw new InvalidOperationException($"pipeline for '{id}' not found");
        }
    }

    private List<IPipelineStep> CreateSteps(PipelineConfig pipelineConfig)
    {
        return pipelineConfig.Steps
            .Select(CreateStep)
            .ToList();
    }

    private IPipelineStep CreateStep(StepConfig stepConfig)
    {
        return new PipelineStep(
            stepConfig.Id,
            stepConfig.DisplayName,
            stepConfig.Input ?? new List<InputConfig>(),
            stepConfig.Output ?? new List<OutputConfig>(),
            CreateProcess(stepConfig));
    }

    private IPipelineProcess CreateProcess(StepConfig stepConfig)
    {
        var processConfig = stepConfig.ProcessId != null ? PipelineProcessConfig.Processes.GetProcessConfig(stepConfig.ProcessId) : null;
        if (processConfig != null)
        {
            var objectType = Type.GetType(processConfig.Implementation);
            if (objectType != null)
            {
                var processInstance = Activator.CreateInstance(objectType) as IPipelineProcess;
                if (processInstance != null)
                {
                    InitializeProcess(objectType, processInstance, processConfig.DataHandlingConfig, GenerateProcessConfig(processConfig.DefaultConfig, stepConfig.ProcessConfigOverwrites));

                    return processInstance;
                }
            }
        }

        throw new InvalidOperationException($"failed to create process instance for '{stepConfig.ProcessId}'");
    }

    private void InitializeProcess(Type processType, IPipelineProcess process, DataHandlingConfig dataHandlingConfig, Dictionary<string, string> processConfig)
    {
        var methods = processType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        InitializeProcess(process, methods, this.configuration);
        InitializeProcess(process, methods, this.cancellationToken);
        InitializeProcess(process, methods, dataHandlingConfig);
        InitializeProcess(process, methods, processConfig);
    }

    private void InitializeProcess(IPipelineProcess process, MethodInfo[] methods, IConfiguration? configuration)
    {
        if (configuration != null)
        {
            methods
            .Where(m => m.GetCustomAttributes(typeof(PipelineProcessInitializeAttribute), true).Length != 0)
            .Where(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IConfiguration))
            .ToList()
            .ForEach(m => m.Invoke(process, new object[] { configuration }));
        }
        else
        {
            logger.LogWarning("No application configuration provided. Skipping pipeline process initialization with IConfiguration.");
        }
    }

    private void InitializeProcess(IPipelineProcess process, MethodInfo[] methods, CancellationToken? cancellationToken)
    {
        if (cancellationToken != null)
        {
            methods
            .Where(m => m.GetCustomAttributes(typeof(PipelineProcessInitializeAttribute), true).Length != 0)
            .Where(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(CancellationToken))
            .ToList()
            .ForEach(m => m.Invoke(process, new object[] { cancellationToken }));
        }
        else
        {
            logger.LogWarning("No cancellation token provided. Skipping pipeline process initialization with CancellationToken.");
        }
    }

    private void InitializeProcess(IPipelineProcess process, MethodInfo[] methods, DataHandlingConfig dataHandlingConfig)
    {
        if (dataHandlingConfig != null)
        {
            methods
            .Where(m => m.GetCustomAttributes(typeof(PipelineProcessInitializeAttribute), true).Length != 0)
            .Where(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(DataHandlingConfig))
            .ToList()
            .ForEach(m => m.Invoke(process, new object[] { dataHandlingConfig }));
        }
        else
        {
            logger.LogWarning("No data handling configuration provided. Skipping pipeline process initialization with data handling configuration.");
        }
    }

    private void InitializeProcess(IPipelineProcess process, MethodInfo[] methods, IDictionary<string, string> processConfig)
    {
        if (processConfig != null)
        {
            methods
            .Where(m => m.GetCustomAttributes(typeof(PipelineProcessInitializeAttribute), true).Length != 0)
            .Where(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(IDictionary<string, string>))
            .ToList()
            .ForEach(m => m.Invoke(process, new object[] { processConfig }));
        }
        else
        {
            logger.LogWarning("No process configuration provided. Skipping pipeline process initialization with process configuration.");
        }
    }

    private Dictionary<string, string> GenerateProcessConfig(Dictionary<string, string>? processDefaultConfig, Dictionary<string, string>? processDefaultConfigOverwrites)
    {
        var mergedConfig = processDefaultConfig != null ? new Dictionary<string, string>(processDefaultConfig) : new Dictionary<string, string>();
        if (processDefaultConfigOverwrites != null)
        {
            foreach (var overwrite in processDefaultConfigOverwrites)
            {
                if (mergedConfig.ContainsKey(overwrite.Key))
                {
                    mergedConfig[overwrite.Key] = overwrite.Value;
                }
                else
                {
                    this.logger.LogWarning("Attempted to overwrite non-existing process configuration '{Key}' ==> '{Value}'", overwrite.Key, overwrite.Value);
                }
            }
        }

        return mergedConfig;
    }

    /// <summary>
    /// Builder for creating instances of <see cref="PipelineFactory"/>.
    /// </summary>
    public class PipelineFactoryBuilder
    {
        private PipelineProcessConfig? pipelineProcessConfig;
        private IConfiguration? configuration;
        private CancellationToken? cancellationToken;

        private PipelineFactoryBuilder()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="PipelineFactoryBuilder"/>.
        /// </summary>
        public static PipelineFactoryBuilder Builder()
        {
            return new PipelineFactoryBuilder();
        }

        /// <summary>
        /// Configures the pipeline factory to use a YAML process definition.
        /// </summary>
        /// <param name="processDefinition">The YAML process definition.</param>
        public PipelineFactoryBuilder Yaml(string processDefinition)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            this.pipelineProcessConfig = deserializer.Deserialize<PipelineProcessConfig>(processDefinition);
            return this;
        }

        /// <summary>
        /// Configures the pipeline factory to use a file-based YAML process definition.
        /// </summary>
        /// <param name="path">The file path to the YAML process definition.</param>
        public PipelineFactoryBuilder File(string path)
        {
            var yaml = System.IO.File.ReadAllText(path);
            return Yaml(yaml);
        }

        /// <summary>
        /// Configures the pipeline factory to use an application configuration.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        public PipelineFactoryBuilder Configuration(IConfiguration configuration)
        {
            this.configuration = configuration;
            return this;
        }

        /// <summary>
        /// Configures the pipeline factory to use a cancellation token.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public PipelineFactoryBuilder CancellationToken(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            return this;
        }

        /// <summary>
        /// Builds a new instance of the <see cref="PipelineFactory"/>.
        /// </summary>
        public PipelineFactory Build()
        {
            return new PipelineFactory(this.pipelineProcessConfig, this.configuration, this.cancellationToken);
        }
    }
}
