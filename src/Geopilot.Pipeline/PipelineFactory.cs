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
    internal PipelineProcessConfig PipelineProcessConfig { get; }

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

    /// <inheritdoc />
    public PipelineDefinitionValidationResult ValidateDefinition()
    {
        var definitionErrors = PipelineProcessConfig.Validate();
        if (definitionErrors.HasErrors)
        {
            return new PipelineDefinitionValidationResult(
                $"errors in pipeline definition:{Environment.NewLine}{definitionErrors.ErrorMessage}");
        }

        var processErrors = new HashSet<string>();
        var processes = PipelineProcessConfig.Processes;
        foreach (var pipeline in PipelineProcessConfig.Pipelines)
        {
            var stepResultTypes = pipelineProcessFactory.BuildStepResultTypes(pipeline.Steps, processes);
            foreach (var step in pipeline.Steps)
            {
                try
                {
                    // Use Validate() rather than Build() so validation does not actually construct processes.
                    // Invoking constructors here would leak per-step directories under $TEMP and allocate
                    // HttpClients and other per-process resources on every restart.
                    pipelineProcessFactory
                        .Builder()
                        .StepConfig(step)
                        .StepResultTypes(stepResultTypes)
                        .Processes(processes)
                        .Validate(resourcesDirectory);
                }
                catch (Exception ex)
                {
                    processErrors.Add($"pipeline {pipeline.Id}, step {step.Id}, process {step.ProcessId}, error: {ex.Message}");
                }
            }
        }

        if (processErrors.Count > 0)
        {
            return new PipelineDefinitionValidationResult(
                $"Invalid pipeline processes found:{Environment.NewLine}{string.Join(Environment.NewLine, processErrors)}");
        }

        return PipelineDefinitionValidationResult.Valid;
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
            .OutputActions(stepConfig.OutputActions ?? new List<OutputActionConfig>())
            .StepConditions(stepConfig.Conditions)
            .PipelineDirectory(pipelineTempDirectory)
            .ResourcesDirectory(resourcesDirectory)
            .Process(pipelineProcessFactory.Builder()
                .PipelineId(pipelineId)
                .StepConfig(stepConfig)
                .Processes(PipelineProcessConfig.Processes)
                .PipelineDirectory(pipelineTempDirectory)
                .ResourcesDirectory(resourcesDirectory)
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

    /// <summary>
    /// Creates a builder for a <see cref="PipelineFactory"/>. This is the entry point for creating
    /// pipelines outside the host, for example from a plugin's integration tests.
    /// </summary>
    /// <returns>A new <see cref="PipelineFactoryBuilder"/>.</returns>
    public static PipelineFactoryBuilder Builder() => new PipelineFactoryBuilder();

    /// <summary>
    /// Fluent builder for a <see cref="PipelineFactory"/>. The pipeline definition, the process factory,
    /// the logger factory and the pipeline working directory are required; the resources root is optional.
    /// </summary>
    public class PipelineFactoryBuilder
    {
        private PipelineProcessConfig? pipelineProcessConfig;
        private IPipelineProcessFactory? pipelineProcessFactory;
        private string? pipelineTempDirectory;
        private string? resourcesDirectory;
        private ILoggerFactory? loggerFactory;

        /// <summary>
        /// Supplies the pipeline definition as YAML text.
        /// </summary>
        /// <param name="processDefinition">The YAML pipeline definition.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder Yaml(string processDefinition)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .WithTypeConverter(new LocalizedTextYamlConverter())
                .Build();
            this.pipelineProcessConfig = deserializer.Deserialize<PipelineProcessConfig>(processDefinition);
            return this;
        }

        /// <summary>
        /// Supplies the pipeline definition by reading it from a YAML file.
        /// </summary>
        /// <param name="path">Path to the YAML pipeline definition.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder File(string path)
        {
            var yaml = System.IO.File.ReadAllText(path);
            return Yaml(yaml);
        }

        /// <summary>
        /// Supplies the process factory used to instantiate the process of each step.
        /// </summary>
        /// <param name="pipelineProcessFactory">The process factory.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder PipelineProcessFactory(IPipelineProcessFactory pipelineProcessFactory)
        {
            this.pipelineProcessFactory = pipelineProcessFactory;
            return this;
        }

        /// <summary>
        /// Supplies the logger factory used for pipeline and step logging.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder LoggerFactory(ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            return this;
        }

        /// <summary>
        /// Supplies the root directory under which the per-job pipeline working directories are created.
        /// </summary>
        /// <param name="pipelineTempDirectory">The pipeline working directory root.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder PipelineTempDirectory(string pipelineTempDirectory)
        {
            this.pipelineTempDirectory = pipelineTempDirectory;
            return this;
        }

        /// <summary>
        /// Supplies the root directory that <c>${file(path)}</c> references resolve against.
        /// </summary>
        /// <param name="resourcesDirectory">The resources root, or <see langword="null"/> when the definition uses no file references.</param>
        /// <returns>The same builder instance.</returns>
        public PipelineFactoryBuilder ResourcesDirectory(string? resourcesDirectory)
        {
            this.resourcesDirectory = resourcesDirectory;
            return this;
        }

        /// <summary>
        /// Builds the configured <see cref="PipelineFactory"/>.
        /// </summary>
        /// <returns>The configured factory.</returns>
        /// <exception cref="InvalidOperationException">A required part was not supplied.</exception>
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
