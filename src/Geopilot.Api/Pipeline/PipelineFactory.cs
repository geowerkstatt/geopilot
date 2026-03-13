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
            return Pipeline.Builder()
                .Id(pipelineConfig.Id)
                .DisplayName(pipelineConfig.DisplayName)
                .Steps(CreateSteps(pipelineConfig, pipelineTempDirectory, jobId))
                .Parameters(pipelineConfig.Parameters)
                .DeliveryCondition(pipelineConfig.DeliveryCondition)
                .File(file)
                .LoggerFactory(this.loggerFactory)
                .PipelineDirectory(pipelineTempDirectory)
                .JobId(jobId)
                .Build();
        }
        else
        {
            throw new InvalidOperationException($"pipeline for '{id}' not found");
        }
    }

    private List<IPipelineStep> CreateSteps(PipelineConfig pipelineConfig, string pipelineTempDirectory, Guid jobId)
    {
        return pipelineConfig.Steps
            .Select(s => CreateStep(s, pipelineTempDirectory, jobId) as IPipelineStep)
            .ToList();
    }

    private PipelineStep CreateStep(StepConfig stepConfig, string pipelineTempDirectory, Guid jobId)
    {
        return PipelineStep.Builder()
            .Id(stepConfig.Id)
            .DisplayName(stepConfig.DisplayName)
            .InputConfig(stepConfig.Input ?? new List<InputConfig>())
            .OutputConfig(stepConfig.Output ?? new List<OutputConfig>())
            .StepConditions(stepConfig.Conditions)
            .Process(pipelineProcessFactory.CreateProcess(stepConfig, PipelineProcessConfig.Processes, pipelineTempDirectory, jobId))
            .LoggerFactory(loggerFactory)
            .JobId(jobId)
            .Build();
    }

    internal static PipelineFactoryBuilder Builder() => new PipelineFactoryBuilder();

    internal class PipelineFactoryBuilder
    {
        private PipelineProcessConfig? pipelineProcessConfig;
        private IPipelineProcessFactory? pipelineProcessFactory;
        private ILoggerFactory? loggerFactory;
        private IDirectoryProvider? directoryProvider;

        public PipelineFactoryBuilder Yaml(string processDefinition)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            this.pipelineProcessConfig = deserializer.Deserialize<PipelineProcessConfig>(processDefinition);
            return this;
        }

        public PipelineFactoryBuilder File(string path)
        {
            var yaml = System.IO.File.ReadAllText(path);
            return Yaml(yaml);
        }

        public PipelineFactoryBuilder PipelineProcessFactory(IPipelineProcessFactory pipelineProcessFactory)
        {
            this.pipelineProcessFactory = pipelineProcessFactory;
            return this;
        }

        public PipelineFactoryBuilder LoggerFactory(ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            return this;
        }

        public PipelineFactoryBuilder DirectoryProvider(IDirectoryProvider? directoryProvider)
        {
            this.directoryProvider = directoryProvider;
            return this;
        }

        public PipelineFactory Build()
        {
            if (this.pipelineProcessFactory == null)
                throw new InvalidOperationException("Pipeline process factory is required but was not provided.");
            if (this.loggerFactory == null)
                throw new InvalidOperationException("Logger factory is required but was not provided.");
            if (this.directoryProvider == null)
                throw new InvalidOperationException("Directory provider is required but was not provided.");

            return new PipelineFactory(pipelineProcessConfig, pipelineProcessFactory, loggerFactory, directoryProvider);
        }
    }
}
