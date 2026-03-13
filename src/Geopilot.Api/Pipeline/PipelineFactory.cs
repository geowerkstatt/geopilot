using Geopilot.Api.FileAccess;
using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
using Geopilot.PipelineCore.Pipeline;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Geopilot.Api.Pipeline;

/// <summary>
/// Factory for creating <see cref="Pipeline"/> instances from YAML configuration.
/// </summary>
public class PipelineFactory : IPipelineFactory
{
    private readonly ILogger logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly IDirectoryProvider directoryProvider;

    /// <summary>
    /// The pipeline process configuration used to create pipelines.
    /// </summary>
    public PipelineProcessConfig PipelineProcessConfig { get; }

    private IPipelineProcessFactory pipelineProcessFactory;

    private PipelineFactory(
        PipelineProcessConfig? pipelineProcessConfig,
        IPipelineProcessFactory pipelineProcessFactory,
        ILoggerFactory loggerFactory,
        IDirectoryProvider directoryProvider)
    {
        this.PipelineProcessConfig = pipelineProcessConfig ?? throw new InvalidOperationException("Missing pipeline process configuration.");
        this.pipelineProcessFactory = pipelineProcessFactory;

        this.loggerFactory = loggerFactory;
        this.directoryProvider = directoryProvider;
        this.logger = loggerFactory.CreateLogger<PipelineFactory>();
    }

    /// <inheritdoc />
    public List<PipelineConfig> Pipelines => PipelineProcessConfig.Pipelines;

    /// <inheritdoc />
    public IPipeline CreatePipeline(string id, IPipelineTransferFile file, Guid jobId)
    {
        var pipelineConfig = PipelineProcessConfig.Pipelines.Find(p => p.Id == id);

        if (pipelineConfig != null)
        {
            var pipelineTempDirectory = directoryProvider.GetPipelineDirectoryPath(jobId);
            return new Pipeline(
                pipelineConfig.Id,
                pipelineConfig.DisplayName,
                CreateSteps(pipelineConfig, pipelineTempDirectory),
                pipelineConfig.Parameters,
                pipelineConfig.DeliveryCondition,
                file,
                this.loggerFactory,
                pipelineTempDirectory);
        }
        else
        {
            throw new InvalidOperationException($"pipeline for '{id}' not found");
        }
    }

    private List<IPipelineStep> CreateSteps(PipelineConfig pipelineConfig, string pipelineTempDirectory)
    {
        return pipelineConfig.Steps
            .Select(s => CreateStep(s, pipelineTempDirectory) as IPipelineStep)
            .ToList();
    }

    private PipelineStep CreateStep(StepConfig stepConfig, string pipelineTempDirectory)
    {
        return new PipelineStep(
            stepConfig.Id,
            stepConfig.DisplayName,
            stepConfig.Input ?? new List<InputConfig>(),
            stepConfig.Output ?? new List<OutputConfig>(),
            stepConfig.Conditions,
            pipelineProcessFactory.CreateProcess(stepConfig, PipelineProcessConfig.Processes, pipelineTempDirectory),
            loggerFactory);
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
        private IPipelineProcessFactory? pipelineProcessFactory;
        private ILoggerFactory? loggerFactory;
        private IDirectoryProvider? directoryProvider;

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
        /// Sets the pipeline process factory to be used for creating pipeline processes.
        /// </summary>
        /// <param name="pipelineProcessFactory">The factory instance that will be used to create pipeline processes. Cannot be null.</param>
        /// <returns>The current <see cref="PipelineFactoryBuilder"/> instance for method chaining.</returns>
        public PipelineFactoryBuilder PipelineProcessFactory(IPipelineProcessFactory pipelineProcessFactory)
        {
            this.pipelineProcessFactory = pipelineProcessFactory;
            return this;
        }

        /// <summary>
        /// Sets the logger factory to be used for creating pipeline processes.
        /// </summary>
        /// <param name="loggerFactory">The factory instance that will be used to create loggers. Cannot be null.</param>
        /// <returns>The current <see cref="PipelineFactoryBuilder"/> instance for method chaining.</returns>
        public PipelineFactoryBuilder LoggerFactory(ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            return this;
        }

        /// <summary>
        /// Sets the directory provider to be used for accessing file system directories.
        /// </summary>
        /// <param name="directoryProvider">The directory provider instance to use. Cannot be null.</param>
        /// <returns>The current <see cref="PipelineFactoryBuilder"/> instance for method chaining.</returns>
        public PipelineFactoryBuilder DirectoryProvider(IDirectoryProvider? directoryProvider)
        {
            this.directoryProvider = directoryProvider;
            return this;
        }

        /// <summary>
        /// Builds a new instance of the <see cref="PipelineFactory"/>.
        /// </summary>
        public PipelineFactory Build()
        {
            if (this.pipelineProcessFactory != null && this.loggerFactory != null && this.directoryProvider != null)
            {
                return new PipelineFactory(pipelineProcessConfig, pipelineProcessFactory, loggerFactory, directoryProvider);
            }
            else
            {
                throw new InvalidOperationException($"Pipeline factory could not be created. Missing required dependencies: Pipeline Process Factory: {this.pipelineProcessFactory != null}, Logger Factory: {this.loggerFactory != null}, Directory Provider: {this.directoryProvider != null}.");
            }
        }
    }
}
