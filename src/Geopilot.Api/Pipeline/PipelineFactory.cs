using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Factory for creating <see cref="Pipeline"/> instances from YAML configuration.
/// </summary>
public class PipelineFactory : IPipelineFactory
{
    private readonly ILogger<PipelineFactory> logger;

    /// <summary>
    /// The pipeline process configuration used to create pipelines.
    /// </summary>
    public PipelineProcessConfig PipelineProcessConfig { get; }

    private IConfiguration? configuration;

    private PipelineFactory(
        PipelineProcessConfig? pipelineProcessConfig,
        IConfiguration? configuration)
    {
        this.PipelineProcessConfig = pipelineProcessConfig ?? throw new InvalidOperationException("Missing pipeline process configuration.");
        this.configuration = configuration;

        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        this.logger = factory.CreateLogger<PipelineFactory>();
    }

    /// <inheritdoc />
    public IPipeline CreatePipeline(string id)
    {
        return CreatePipeline(id, CancellationToken.None);
    }

    /// <inheritdoc />
    public IPipeline CreatePipeline(string id, CancellationToken cancellationToken)
    {
        var pipelineConfig = PipelineProcessConfig.Pipelines.Find(p => p.Id == id);

        if (pipelineConfig != null)
        {
            return new Pipeline(pipelineConfig.Id, pipelineConfig.DisplayName, CreateSteps(pipelineConfig, cancellationToken), pipelineConfig.Parameters);
        }
        else
        {
            throw new InvalidOperationException($"pipeline for '{id}' not found");
        }
    }

    private List<IPipelineStep> CreateSteps(PipelineConfig pipelineConfig, CancellationToken cancellationToken)
    {
        return pipelineConfig.Steps
            .Select(s => CreateStep(s, cancellationToken) as IPipelineStep)
            .ToList();
    }

    private PipelineStep CreateStep(StepConfig stepConfig, CancellationToken cancellationToken)
    {
        return new PipelineStep(
            stepConfig.Id,
            stepConfig.DisplayName,
            stepConfig.Input ?? new List<InputConfig>(),
            stepConfig.Output ?? new List<OutputConfig>(),
            CreateProcess(stepConfig, cancellationToken));
    }

    private IPipelineProcess CreateProcess(StepConfig stepConfig, CancellationToken cancellationToken)
    {
        var processConfig = stepConfig.ProcessId != null ? PipelineProcessConfig.Processes.GetProcessConfig(stepConfig.ProcessId) : null;
        if (processConfig != null)
        {
            var objectType = Type.GetType(processConfig.Implementation);
            if (objectType != null)
            {
                var processInstance = Activator.CreateInstance(objectType);
                if (processInstance != null)
                {
                    InitializeProcess(objectType, processInstance, processConfig.DataHandlingConfig, GenerateProcessConfig(processConfig.DefaultConfig, stepConfig.ProcessConfigOverwrites), cancellationToken);

                    return processInstance;
                }
            }
        }

        throw new InvalidOperationException($"failed to create process instance for '{stepConfig.ProcessId}'");
    }

    private void InitializeProcess(Type processType, object process, DataHandlingConfig dataHandlingConfig, Parameterization processConfig)
    {
        var initMethods = processType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttributes(typeof(PipelineProcessInitializeAttribute), true).Length > 0)
            .ToList();
        initMethods
            .ForEach(m =>
            {
                var parameters = m.GetParameters()
                    .Select(p => p.ParameterType)
                    .Select(t => GenerateParameter(t, dataHandlingConfig, processConfig, cancellationToken))
                    .ToArray();
                m.Invoke(process, parameters);
            });
    }

    private object? GenerateParameter(Type parameterType, DataHandlingConfig dataHandlingConfig, Parameterization processConfig, CancellationToken cancellationToken)
    {
        if (parameterType == typeof(IConfiguration))
        {
            return configuration;
        }
        else if (parameterType == typeof(CancellationToken))
        {
            return cancellationToken;
        }
        else if (parameterType == typeof(DataHandlingConfig))
        {
            return dataHandlingConfig;
        }
        else if (parameterType == typeof(Parameterization))
        {
            return processConfig;
        }
        else
        {
            logger.LogWarning($"Process initialization: No suitable parameter found for type '{parameterType}' with name '{parameterType.Name}' Initializing with null.");
            return null;
        }
    }

    private Parameterization GenerateProcessConfig(Parameterization? processDefaultConfig, Parameterization? processDefaultConfigOverwrites)
    {
        var mergedConfig = processDefaultConfig != null ? new Parameterization(processDefaultConfig) : new Parameterization();
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
    /// Creates a new instance of the <see cref="PipelineFactoryBuilder"/>.
    /// </summary>
    public static PipelineFactoryBuilder Builder()
    {
        return new PipelineFactoryBuilder();
    }

    /// <summary>
    /// Builder for creating instances of <see cref="PipelineFactory"/>.
    /// </summary>
    public class PipelineFactoryBuilder
    {
        private PipelineProcessConfig? pipelineProcessConfig;
        private IConfiguration? configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipelineFactoryBuilder"/> class.
        /// </summary>
        public PipelineFactoryBuilder()
        {
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
        /// Builds a new instance of the <see cref="PipelineFactory"/>.
        /// </summary>
        public PipelineFactory Build()
        {
            return new PipelineFactory(this.pipelineProcessConfig, this.configuration);
        }
    }
}
