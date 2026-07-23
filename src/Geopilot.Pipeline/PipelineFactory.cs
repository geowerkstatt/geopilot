using Geopilot.Pipeline.Config;
using Geopilot.Pipeline.Process;
using Geopilot.PipelineCore.Pipeline;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Geopilot.Pipeline;

/// <summary>
/// Factory for creating <see cref="Pipeline"/> instances from YAML configuration.
/// </summary>
public class PipelineFactory : IPipelineFactory
{
    private readonly ILogger logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly string pipelineTempDirectory;
    private readonly string? resourcesDirectory;

    /// <summary>
    /// The pipeline process configuration used to create pipelines.
    /// </summary>
    public PipelineProcessConfig PipelineProcessConfig { get; }

    private IPipelineProcessFactory pipelineProcessFactory;

    private PipelineFactory(
        PipelineProcessConfig? pipelineProcessConfig,
        IPipelineProcessFactory pipelineProcessFactory,
        string pipelineTempDirectory,
        string? resourcesDirectory,
        ILoggerFactory loggerFactory)
    {
        this.PipelineProcessConfig = pipelineProcessConfig ?? throw new InvalidOperationException("Missing pipeline process configuration.");
        this.pipelineProcessFactory = pipelineProcessFactory;
        this.pipelineTempDirectory = pipelineTempDirectory;
        this.resourcesDirectory = resourcesDirectory;

        this.loggerFactory = loggerFactory;
        this.logger = loggerFactory.CreateLogger<PipelineFactory>();
    }

    /// <inheritdoc />
    public List<PipelineConfig> Pipelines => PipelineProcessConfig.Pipelines;

    /// <inheritdoc />
    public IPipeline CreatePipeline(string id, Guid jobId)
    {
        var pipelineConfig = PipelineProcessConfig.Pipelines.Find(p => p.Id == id);

        var jobPipelineDirectory = Path.Combine(pipelineTempDirectory, jobId.ToString());

        if (pipelineConfig != null)
        {
            return Geopilot.Pipeline.Pipeline.Builder()
                .Id(pipelineConfig.Id)
                .DisplayName(pipelineConfig.DisplayName)
                .Steps(CreateSteps(pipelineConfig, jobPipelineDirectory, jobId))
                .DeliveryRestrictions(pipelineConfig.DeliveryRestrictions)
                .Logger(PipelineLogger
                    .Builder()
                    .Logger(loggerFactory.CreateLogger<Geopilot.Pipeline.Pipeline>())
                    .PipelineId(id)
                    .JobId(jobId)
                    .Build())
                .PipelineDirectory(jobPipelineDirectory)
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
            .Select(s => CreateStep(s, pipelineConfig.Id, pipelineTempDirectory, jobId) as IPipelineStep)
            .ToList();
    }

    private PipelineStep CreateStep(StepConfig stepConfig, string pipelineId, string pipelineTempDirectory, Guid jobId)
    {
        return PipelineStep.Builder()
            .Id(stepConfig.Id)
            .DisplayName(stepConfig.DisplayName)
            .Inputs(InputCompiler.Compile(stepConfig.Input ?? new InputConfig()))
            .OutputActions(stepConfig.Output ?? new List<OutputActionConfig>())
            .StepConditions(stepConfig.Conditions)
            .PipelineDirectory(pipelineTempDirectory)
            .ResourcesDirectory(resourcesDirectory)
            .Process(pipelineProcessFactory.Builder()
                .PipelineId(pipelineId)
                .StepConfig(stepConfig)
                .Processes(PipelineProcessConfig.Processes)
                .PipelineDirectory(pipelineTempDirectory)
                .JobId(jobId)
                .Build())
            .Logger(PipelineLogger
                    .Builder()
                    .Logger(loggerFactory.CreateLogger<PipelineStep>())
                    .StepId(stepConfig.Id)
                    .PipelineId(pipelineId)
                    .JobId(jobId)
                    .Build())
            .Build();
    }

    internal static PipelineFactoryBuilder Builder() => new PipelineFactoryBuilder();

    internal class PipelineFactoryBuilder
    {
        private PipelineProcessConfig? pipelineProcessConfig;
        private IPipelineProcessFactory? pipelineProcessFactory;
        private string? pipelineTempDirectory;
        private string? resourcesDirectory;
        private ILoggerFactory? loggerFactory;

        public PipelineFactoryBuilder Yaml(string processDefinition)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .WithTypeConverter(new LocalizedTextYamlConverter())
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

        public PipelineFactoryBuilder PipelineTempDirectory(string pipelineTempDirectory)
        {
            this.pipelineTempDirectory = pipelineTempDirectory;
            return this;
        }

        public PipelineFactoryBuilder ResourcesDirectory(string? resourcesDirectory)
        {
            this.resourcesDirectory = resourcesDirectory;
            return this;
        }

        public PipelineFactory Build()
        {
            if (this.pipelineProcessFactory == null)
                throw new InvalidOperationException("Pipeline process factory is required but was not provided.");
            if (this.loggerFactory == null)
                throw new InvalidOperationException("Logger factory is required but was not provided.");
            if (this.pipelineTempDirectory == null)
                throw new InvalidOperationException("Pipeline temp directory is required but was not provided.");

            return new PipelineFactory(pipelineProcessConfig, pipelineProcessFactory, pipelineTempDirectory, resourcesDirectory, loggerFactory);
        }
    }
}
