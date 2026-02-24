using Geopilot.Api.Pipeline.Config;
using Geopilot.Api.Pipeline.Process;
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

    private IPipelineProcessFactory pipelineProcessFactory;

    private PipelineFactory(
        PipelineProcessConfig? pipelineProcessConfig,
        IPipelineProcessFactory pipelineProcessFactory)
    {
        this.PipelineProcessConfig = pipelineProcessConfig ?? throw new InvalidOperationException("Missing pipeline process configuration.");
        this.pipelineProcessFactory = pipelineProcessFactory;

        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        this.logger = factory.CreateLogger<PipelineFactory>();
    }

    /// <inheritdoc />
    public List<PipelineConfig> Pipelines => PipelineProcessConfig.Pipelines;

    /// <inheritdoc />
    public IPipeline CreatePipeline(string id)
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
            .Select(s => CreateStep(s) as IPipelineStep)
            .ToList();
    }

    private PipelineStep CreateStep(StepConfig stepConfig)
    {
        return new PipelineStep(
            stepConfig.Id,
            stepConfig.DisplayName,
            stepConfig.Input ?? new List<InputConfig>(),
            stepConfig.Output ?? new List<OutputConfig>(),
            pipelineProcessFactory.CreateProcess(stepConfig, PipelineProcessConfig.Processes));
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
        /// Builds a new instance of the <see cref="PipelineFactory"/>.
        /// </summary>
        public PipelineFactory Build()
        {
            if (this.pipelineProcessFactory != null)
            {
                return new PipelineFactory(this.pipelineProcessConfig, pipelineProcessFactory);
            }
            else
            {
                throw new InvalidOperationException("Pipeline process factory is required to build a pipeline factory.");
            }
        }
    }
}
